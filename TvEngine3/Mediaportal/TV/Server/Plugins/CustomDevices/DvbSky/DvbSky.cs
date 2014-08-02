﻿#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Runtime.InteropServices;
using System.Threading;
using DirectShowLib;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.DvbSky
{
  /// <summary>
  /// A class for handling conditional access, DiSEqC and remote controls for DVBSky tuners and
  /// clones (eg. certain newer TechnoTrend and Mystique models). Actually with the exception of
  /// the property set GUIDs, the DVBSky conditional access interface is [almost] identical to the
  /// NetUP conditional access interface, and their DiSEqC interface is identical to the Conexant
  /// interface.
  /// </summary>
  public class DvbSky : BaseCustomDevice, IConditionalAccessProvider, IConditionalAccessMenuActions, IDiseqcDevice, IRemoteControlListener
  {
    #region enums

    private enum BdaExtensionProperty
    {
      /// For sending and receiving DiSEqC messages.
      DiseqcMessage = 0,
      /// For receiving remote keypresses.
      RemoteCode,
      /// For passing blind scan parameters and commands.
      BlindScanCommand,
      /// For retrieving blind scan results.
      BlindScanData,
      /// For getting or setting DVB-C2/T2 PLP information.
      DvbPlpId,
      /// For getting the MAC address for tuner 1.
      MacAddressTuner1,
      /// For getting the MAC address for tuner 2.
      MacAddressTuner2
    }

    /// <summary>
    /// Remote control RC-5 system addresses.
    /// </summary>
    private enum DvbSkyRemoteType
    {
      DvbSky = 0,
      TechnoTrend = 21
    }

    /// <summary>
    /// Remote codes for DVBSky products.
    /// </summary>
    /// <remarks>
    /// Image: http://www.dvbshop.net/bilder/produkte/gross/Mystique-SaTiX-S2-Sky-PCI-USALS-DiseqC-12-Win-Linux-Remote_b2.jpg
    /// Testing: untested, based on SDK documentation
    /// </remarks>
    private enum DvbSkyRemoteCodeDvbSky : byte
    {
      Zero = 0,
      One,
      Two,
      Three,
      Four,
      Five,
      Six,
      Seven,
      Eight,
      Nine,
      Mute, // 10
      Stop,
      Exit,
      Okay,
      Screenshot,
      PictureInPicture,
      Right,
      Left,
      Favourite,
      Info, // 19

      Pause = 22,
      Play,

      Record = 31,
      Up,
      Down,

      Power = 37,
      Rewind,
      SkipForward,

      Recall = 41,

      Menu = 43,
      Epg,
      Zoom
    }

    /// <summary>
    /// Remote codes for TechnoTrend clones.
    /// </summary>
    /// <remarks>
    /// Image: http://www.dvbshop.net/bilder/produkte/gross/TechnoTrend-Budget-S-1500-inkl-CI-Common-Int-TT-Viewer_b2.jpg
    /// Testing: CT2-4500 CI
    /// </remarks>
    private enum DvbSkyRemoteCodeTechnoTrend : byte
    {
      Power = 1,
      Recall,           // icon: two arrows in a circle
      One,
      Two,
      Three,
      Four,
      Five,
      Six,
      Seven,
      Eight,  // 10
      Nine,
      Zero,
      Up,
      Left,
      Okay,
      Right,
      Down,
      Info,
      Exit,
      Red,    // 20
      Green,
      Yellow,
      Blue,
      Mute,
      Teletext,
      Source, // 26     // text: TV/radio; icon: musical note

      Epg = 34,
      ChannelUp,
      ChannelDown,
      VolumeUp,
      VolumeDown,

      Record = 58,
      Play,
      Stop,
      Rewind,
      Pause,
      FastForward
    }

    #endregion

    #region constants

    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_GENERAL = new Guid(0x03cbdcb9, 0x36dc, 0x4072, 0xac, 0x42, 0x2f, 0x94, 0xf4, 0xec, 0xa0, 0x5e);
    private static readonly Guid BDA_EXTENSION_PROPERTY_SET_CA = new Guid(0x4fdc5d3a, 0x1543, 0x479e, 0x9f, 0xc3, 0xb7, 0xdb, 0xa4, 0x73, 0xfb, 0x95);

    private const int INSTANCE_SIZE = 32;   // The size of a property instance (KSP_NODE) parameter.
    private const int MAC_ADDRESS_LENGTH = 6;
    private const int REMOTE_CONTROL_DATA_SIZE = 4;

    private const int REMOTE_CONTROL_LISTENER_THREAD_WAIT_TIME = 100;     // unit = ms

    #endregion

    #region variables

    private bool _isDvbSky = false;
    private Conexant.Conexant _conexantInterface = null;
    private NetUp.NetUp _netUpInterface = null;
    private IKsPropertySet _propertySet = null;

    private IntPtr _instanceBuffer = IntPtr.Zero;

    private bool _isRemoteControlInterfaceOpen = false;
    private IntPtr _remoteControlBuffer = IntPtr.Zero;
    private Thread _remoteControlListenerThread = null;
    private AutoResetEvent _remoteControlListenerThreadStopEvent = null;

    #endregion

    private void ReadMacAddresses()
    {
      this.LogDebug("DVBSky: read MAC addresses");

      IntPtr dataBuffer = Marshal.AllocCoTaskMem(MAC_ADDRESS_LENGTH);
      try
      {
        BdaExtensionProperty[] properties = new BdaExtensionProperty[] { BdaExtensionProperty.MacAddressTuner1, BdaExtensionProperty.MacAddressTuner2 };
        string[] propertyNames = new string[] { "tuner 1 MAC address", "tuner 2 MAC address" };

        KSPropertySupport support;
        byte[] previousAddress = null;
        for (int i = 0; i < properties.Length; i++)
        {
          int hr = _propertySet.QuerySupported(BDA_EXTENSION_PROPERTY_SET_GENERAL, (int)BdaExtensionProperty.MacAddressTuner1, out support);
          if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Get))
          {
            this.LogWarn("DVBSky: {0} property not supported, hr = 0x{1:x} ({2})", propertyNames[i], hr, HResult.GetDXErrorString(hr));
          }
          else
          {
            for (int b = 0; b < MAC_ADDRESS_LENGTH; b++)
            {
              Marshal.WriteByte(dataBuffer, b, 0);
            }
            int returnedByteCount;
            hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET_GENERAL, (int)properties[i], _instanceBuffer, INSTANCE_SIZE, dataBuffer, MAC_ADDRESS_LENGTH, out returnedByteCount);
            if (hr != (int)HResult.Severity.Success || returnedByteCount != MAC_ADDRESS_LENGTH)
            {
              this.LogWarn("DVBSky: failed to read {0}, hr = 0x{1:x} ({2}), byte count = {3}", propertyNames[i], hr, HResult.GetDXErrorString(hr), returnedByteCount);
            }
            else
            {
              byte[] address = new byte[MAC_ADDRESS_LENGTH];
              Marshal.Copy(dataBuffer, address, 0, MAC_ADDRESS_LENGTH);
              if (previousAddress != null)
              {
                for (int b = 0; b < MAC_ADDRESS_LENGTH; b++)
                {
                  if (address[b] != previousAddress[b])
                  {
                    this.LogDebug("  {0} = {1}", propertyNames[i], BitConverter.ToString(address).ToLowerInvariant());
                    break;
                  }
                }
              }
              else
              {
                this.LogDebug("  {0} = {1}", propertyNames[i], BitConverter.ToString(address).ToLowerInvariant());
              }
              previousAddress = address;
            }
          }
        }
      }
      finally
      {
        Marshal.FreeCoTaskMem(dataBuffer);
      }
    }

    #region remote control listener thread

    /// <summary>
    /// Start a thread to listen for remote control commands.
    /// </summary>
    private void StartRemoteControlListenerThread()
    {
      // Don't start a thread if the interface has not been opened.
      if (!_isRemoteControlInterfaceOpen)
      {
        return;
      }

      // Kill the existing thread if it is in "zombie" state.
      if (_remoteControlListenerThread != null && !_remoteControlListenerThread.IsAlive)
      {
        StopRemoteControlListenerThread();
      }
      if (_remoteControlListenerThread == null)
      {
        this.LogDebug("DVBSky: starting new remote control listener thread");
        _remoteControlListenerThreadStopEvent = new AutoResetEvent(false);
        _remoteControlListenerThread = new Thread(new ThreadStart(RemoteControlListener));
        _remoteControlListenerThread.Name = "DVBSky remote control listener";
        _remoteControlListenerThread.IsBackground = true;
        _remoteControlListenerThread.Priority = ThreadPriority.Lowest;
        _remoteControlListenerThread.Start();
      }
    }

    /// <summary>
    /// Stop the thread that listens for remote control commands.
    /// </summary>
    private void StopRemoteControlListenerThread()
    {
      if (_remoteControlListenerThread != null)
      {
        if (!_remoteControlListenerThread.IsAlive)
        {
          this.LogWarn("DVBSky: aborting old remote control listener thread");
          _remoteControlListenerThread.Abort();
        }
        else
        {
          _remoteControlListenerThreadStopEvent.Set();
          if (!_remoteControlListenerThread.Join(REMOTE_CONTROL_LISTENER_THREAD_WAIT_TIME * 2))
          {
            this.LogWarn("DVBSky: failed to join remote control listener thread, aborting thread");
            _remoteControlListenerThread.Abort();
          }
        }
        _remoteControlListenerThread = null;
        if (_remoteControlListenerThreadStopEvent != null)
        {
          _remoteControlListenerThreadStopEvent.Close();
          _remoteControlListenerThreadStopEvent = null;
        }
      }
    }

    /// <summary>
    /// Thread function for receiving remote control commands.
    /// </summary>
    private void RemoteControlListener()
    {
      this.LogDebug("DVBSky: remote control listener thread start polling");
      int hr;
      int returnedByteCount;
      try
      {
        while (!_remoteControlListenerThreadStopEvent.WaitOne(REMOTE_CONTROL_LISTENER_THREAD_WAIT_TIME))
        {
          for (int i = 0; i < REMOTE_CONTROL_DATA_SIZE; i++)
          {
            Marshal.WriteByte(_remoteControlBuffer, i, 0);
          }
          hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET_GENERAL, (int)BdaExtensionProperty.RemoteCode,
            _instanceBuffer, INSTANCE_SIZE,
            _remoteControlBuffer, REMOTE_CONTROL_DATA_SIZE,
            out returnedByteCount
          );
          if (hr != (int)HResult.Severity.Success || returnedByteCount != REMOTE_CONTROL_DATA_SIZE)
          {
            this.LogError("DVBSky: failed to read remote code, hr = 0x{0:x} ({1}), byte count = {2}", hr, HResult.GetDXErrorString(hr), returnedByteCount);
          }
          else
          {
            int code = Marshal.ReadInt32(_remoteControlBuffer, 0);
            if ((code & 0xff) != 0xff)
            {
              // Standard RC-5 code structure.
              int fieldBit = (code & 0x1000) >> 12;
              int toggleBit = (code & 0x800) >> 11;
              int systemAddress = (code & 0x7c0) >> 6;
              int rc5Code = code & 0x3f;
              string codeName;
              if (systemAddress == (int)DvbSkyRemoteType.TechnoTrend)
              {
                codeName = ((DvbSkyRemoteCodeTechnoTrend)rc5Code).ToString();
              }
              else if (systemAddress == (int)DvbSkyRemoteType.DvbSky)
              {
                codeName = ((DvbSkyRemoteCodeDvbSky)rc5Code).ToString();
              }
              else
              {
                codeName = rc5Code.ToString();
              }
              this.LogDebug("DVBSky: remote control key press, field bit = {0}, toggle bit = {1} system address = {2}, code = {3}", fieldBit, toggleBit, systemAddress, codeName);
            }
          }
        }
      }
      catch (ThreadAbortException)
      {
      }
      catch (Exception ex)
      {
        this.LogError(ex, "DVBSky: remote control listener thread exception");
        return;
      }
      this.LogDebug("DVBSky: remote control listener thread stop polling");
    }

    #endregion

    #region ICustomDevice members

    /// <summary>
    /// The loading priority for this extension.
    /// </summary>
    public override byte Priority
    {
      get
      {
        return 75;
      }
    }

    /// <summary>
    /// A human-readable name for the extension. This could be a manufacturer or reseller name, or
    /// even a model name and/or number.
    /// </summary>
    public override string Name
    {
      get
      {
        return "DVBSky";
      }
    }

    /// <summary>
    /// Attempt to initialise the extension-specific interfaces used by the class. If
    /// initialisation fails, the <see ref="ICustomDevice"/> instance should be disposed
    /// immediately.
    /// </summary>
    /// <param name="tunerExternalId">The external identifier for the tuner.</param>
    /// <param name="tunerType">The tuner type (eg. DVB-S, DVB-T... etc.).</param>
    /// <param name="context">Context required to initialise the interface.</param>
    /// <returns><c>true</c> if the interfaces are successfully initialised, otherwise <c>false</c></returns>
    public override bool Initialise(string tunerExternalId, CardType tunerType, object context)
    {
      this.LogDebug("DVBSky: initialising");

      if (_isDvbSky)
      {
        this.LogWarn("DVBSky: extension already initialised");
        return true;
      }

      IBaseFilter tunerFilter = context as IBaseFilter;
      if (tunerFilter == null)
      {
        this.LogDebug("DVBSky: context is not a filter");
        return false;
      }

      _conexantInterface = new Conexant.Conexant(BDA_EXTENSION_PROPERTY_SET_GENERAL);
      if (!_conexantInterface.Initialise(tunerExternalId, tunerType, context))
      {
        this.LogDebug("DVBSky: base Conexant interface not supported");
        return false;
      }
      this.LogInfo("DVBSky: extension supported");
      _isDvbSky = true;
      _instanceBuffer = Marshal.AllocCoTaskMem(INSTANCE_SIZE);
      _propertySet = DsFindPin.ByDirection(tunerFilter, PinDirection.Input, 0) as IKsPropertySet;
      ReadMacAddresses();

      // This should succeed even if the product does not have a CI.
      _netUpInterface = new NetUp.NetUp(BDA_EXTENSION_PROPERTY_SET_CA);
      if (!_netUpInterface.Initialise(tunerExternalId, tunerType, context))
      {
        this.LogWarn("DVBSky: failed to initialise base NetUP interface");
        _netUpInterface.Dispose();
        _netUpInterface = null;
      }
      return true;
    }

    #endregion

    #region IConditionalAccessProvider members

    /// <summary>
    /// Open the conditional access interface. For the interface to be opened successfully it is expected
    /// that any necessary hardware (such as a CI slot) is connected.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully opened, otherwise <c>false</c></returns>
    public bool OpenConditionalAccessInterface()
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.OpenConditionalAccessInterface();
      }
      return false;
    }

    /// <summary>
    /// Close the conditional access interface.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully closed, otherwise <c>false</c></returns>
    public bool CloseConditionalAccessInterface()
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.CloseConditionalAccessInterface();
      }
      return true;
    }

    /// <summary>
    /// Reset the conditional access interface.
    /// </summary>
    /// <param name="resetTuner">This parameter will be set to <c>true</c> if the tuner must be reset
    ///   for the interface to be completely and successfully reset.</param>
    /// <returns><c>true</c> if the interface is successfully reset, otherwise <c>false</c></returns>
    public bool ResetConditionalAccessInterface(out bool resetTuner)
    {
      resetTuner = false;
      if (_netUpInterface != null)
      {
        return _netUpInterface.ResetConditionalAccessInterface(out resetTuner);
      }
      return false;
    }

    /// <summary>
    /// Determine whether the conditional access interface is ready to receive commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is ready, otherwise <c>false</c></returns>
    public bool IsConditionalAccessInterfaceReady()
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.IsConditionalAccessInterfaceReady();
      }
      return false;
    }

    /// <summary>
    /// Send a command to to the conditional access interface.
    /// </summary>
    /// <param name="channel">The channel information associated with the service which the command relates to.</param>
    /// <param name="listAction">It is assumed that the interface may be able to decrypt one or more services
    ///   simultaneously. This parameter gives the interface an indication of the number of services that it
    ///   will be expected to manage.</param>
    /// <param name="command">The type of command.</param>
    /// <param name="pmt">The program map table for the service.</param>
    /// <param name="cat">The conditional access table for the service.</param>
    /// <returns><c>true</c> if the command is successfully sent, otherwise <c>false</c></returns>
    public bool SendConditionalAccessCommand(IChannel channel, CaPmtListManagementAction listAction, CaPmtCommand command, Pmt pmt, Cat cat)
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.SendConditionalAccessCommand(channel, listAction, command, pmt, cat);
      }
      return false;
    }

    #endregion

    #region IConditionalAccessMenuActions members

    /// <summary>
    /// Set the menu call back delegate.
    /// </summary>
    /// <param name="callBack">The call back delegate.</param>
    public void SetMenuCallBack(IConditionalAccessMenuCallBack callBack)
    {
      if (_netUpInterface != null)
      {
        _netUpInterface.SetMenuCallBack(callBack);
      }
    }

    /// <summary>
    /// Send a request from the user to the CAM to open the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool EnterMenu()
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.EnterMenu();
      }
      return false;
    }

    /// <summary>
    /// Send a request from the user to the CAM to close the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool CloseMenu()
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.CloseMenu();
      }
      return false;
    }

    /// <summary>
    /// Send a menu entry selection from the user to the CAM.
    /// </summary>
    /// <param name="choice">The index of the selection as an unsigned byte value.</param>
    /// <returns><c>true</c> if the selection is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool SelectMenuEntry(byte choice)
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.SelectMenuEntry(choice);
      }
      return false;
    }

    /// <summary>
    /// Send an answer to an enquiry from the user to the CAM.
    /// </summary>
    /// <param name="cancel"><c>True</c> to cancel the enquiry.</param>
    /// <param name="answer">The user's answer to the enquiry.</param>
    /// <returns><c>true</c> if the answer is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool AnswerEnquiry(bool cancel, string answer)
    {
      if (_netUpInterface != null)
      {
        return _netUpInterface.AnswerEnquiry(cancel, answer);
      }
      return false;
    }

    #endregion

    #region IDiseqcDevice members

    /// <summary>
    /// Control whether tone/data burst and 22 kHz legacy tone are used.
    /// </summary>
    /// <param name="toneBurstState">The tone/data burst state.</param>
    /// <param name="tone22kState">The 22 kHz legacy tone state.</param>
    /// <returns><c>true</c> if the tone state is set successfully, otherwise <c>false</c></returns>
    public bool SetToneState(ToneBurst toneBurstState, Tone22k tone22kState)
    {
      if (_conexantInterface != null)
      {
        return _conexantInterface.SetToneState(toneBurstState, tone22kState);
      }
      return false;
    }

    /// <summary>
    /// Send an arbitrary DiSEqC command.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns><c>true</c> if the command is sent successfully, otherwise <c>false</c></returns>
    public bool SendDiseqcCommand(byte[] command)
    {
      if (_conexantInterface != null)
      {
        return _conexantInterface.SendDiseqcCommand(command);
      }
      return false;
    }

    /// <summary>
    /// Retrieve the response to a previously sent DiSEqC command (or alternatively, check for a command
    /// intended for this tuner).
    /// </summary>
    /// <param name="response">The response (or command).</param>
    /// <returns><c>true</c> if the response is read successfully, otherwise <c>false</c></returns>
    public bool ReadDiseqcResponse(out byte[] response)
    {
      if (_conexantInterface != null)
      {
        return _conexantInterface.ReadDiseqcResponse(out response);
      }
      // Not implemented.
      response = null;
      return false;
    }

    #endregion

    #region IRemoteControlListener members

    /// <summary>
    /// Open the remote control interface and start listening for commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully opened, otherwise <c>false</c></returns>
    public bool OpenRemoteControlInterface()
    {
      this.LogDebug("DVBSky: open remote control interface");

      if (!_isDvbSky)
      {
        this.LogWarn("DVBSky: not initialised or interface not supported");
        return false;
      }
      if (_isRemoteControlInterfaceOpen)
      {
        this.LogWarn("DVBSky: conditional access interface is already open");
        return true;
      }

      KSPropertySupport support;
      int hr = _propertySet.QuerySupported(BDA_EXTENSION_PROPERTY_SET_GENERAL, (int)BdaExtensionProperty.RemoteCode, out support);
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Get))
      {
        this.LogDebug("DVBSky: property not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      _remoteControlBuffer = Marshal.AllocCoTaskMem(REMOTE_CONTROL_DATA_SIZE);
      _isRemoteControlInterfaceOpen = true;
      StartRemoteControlListenerThread();

      this.LogDebug("DVBSky: result = success");
      return true;
    }

    /// <summary>
    /// Close the remote control interface and stop listening for commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully closed, otherwise <c>false</c></returns>
    public bool CloseRemoteControlInterface()
    {
      this.LogDebug("DVBSky: close remote control interface");

      StopRemoteControlListenerThread();
      if (_remoteControlBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_remoteControlBuffer);
        _remoteControlBuffer = IntPtr.Zero;
      }

      _isRemoteControlInterfaceOpen = false;
      this.LogDebug("DVBSky: result = success");
      return true;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      if (_isDvbSky)
      {
        CloseRemoteControlInterface();
      }
      Release.ComObject("DVBSky property set", ref _propertySet);
      if (_conexantInterface != null)
      {
        _conexantInterface.Dispose();
        _conexantInterface = null;
      }
      if (_netUpInterface != null)
      {
        _netUpInterface.Dispose();
        _netUpInterface = null;
      }
      if (_instanceBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_instanceBuffer);
        _instanceBuffer = IntPtr.Zero;
      }
      _isDvbSky = false;
    }

    #endregion
  }
}