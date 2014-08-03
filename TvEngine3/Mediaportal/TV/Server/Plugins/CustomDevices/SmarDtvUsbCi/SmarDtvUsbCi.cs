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
using System.Collections.ObjectModel;
using DirectShowLib;
using Mediaportal.TV.Server.Plugins.Base.Interfaces;
using Mediaportal.TV.Server.Plugins.TunerExtension.SmarDtvUsbCi.Config;
using Mediaportal.TV.Server.Plugins.TunerExtension.SmarDtvUsbCi.Product;
using Mediaportal.TV.Server.Plugins.TunerExtension.SmarDtvUsbCi.Service;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.TVControl.Interfaces.Services;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Helper;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.SmarDtvUsbCi
{
  /// <summary>
  /// A class for handling conditional access with the Hauppauge WinTV-CI and TerraTec USB CI. Both
  /// devices are based on an OEM design by SmarDTV.
  /// </summary>
  public class SmarDtvUsbCi : BaseCustomDevice, IDirectShowAddOnDevice, IConditionalAccessProvider, IConditionalAccessMenuActions, ITvServerPlugin, ITvServerPluginCommunciation
  {
    #region variables

    private bool _isSmarDtvUsbCi = false;
    private bool _isCaInterfaceOpen = false;
    #pragma warning disable 0414
    private bool _isCamPresent = false;
    #pragma warning restore 0414
    private bool _isCamReady = false;
    private SmarDtvUsbCiState _ciState = SmarDtvUsbCiState.Empty;

    private ISmarDtvUsbCiProduct _product = null;
    private IBaseFilter _ciFilter = null;
    private IFilterGraph2 _graph = null;
    private bool _isFilterInGraph = false;

    // Call backs
    private IConditionalAccessMenuCallBack _caMenuCallBack = null;
    private object _caMenuCallBackLock = new object();
    private SmarDtvUsbCiCallBack _ciCallBack;

    #endregion

    #region delegate implementations

    /// <summary>
    /// Invoked by the driver when the CI slot state changes.
    /// </summary>
    /// <param name="ciFilter">The CI filter.</param>
    /// <param name="state">The new state of the slot.</param>
    /// <returns>an HRESULT indicating whether the state change was successfully handled</returns>
    private int OnCiState(IBaseFilter ciFilter, SmarDtvUsbCiState state)
    {
      this.LogInfo("SmarDTV USB CI: CI state change call back");
      this.LogInfo("  old state  = {0}", _ciState);
      this.LogInfo("  new state  = {0}", state);
      _ciState = state;

      _isCamReady = false;
      if (state == SmarDtvUsbCiState.Empty)
      {
        _isCamPresent = false;
      }
      else if (state == SmarDtvUsbCiState.CamPresent)
      {
        _isCamPresent = true;
      }
      return 0;
    }

    /// <summary>
    /// Invoked by the driver when application information is received from a CAM.
    /// </summary>
    /// <param name="ciFilter">The CI filter.</param>
    /// <param name="info">The application information.</param>
    /// <returns>an HRESULT indicating whether the application information was successfully processed</returns>
    private int OnApplicationInfo(IBaseFilter ciFilter, ref SmarDtvUsbCiApplicationInfo info)
    {
      this.LogInfo("SmarDTV USB CI: application information call back");
      this.LogDebug("  type         = {0}", info.ApplicationType);
      // Note: current drivers seem to have a bug that causes only the first byte in the manufacturer and code
      // fields to be available.
      this.LogDebug("  manufacturer = 0x{0:x4}", info.Manufacturer);
      this.LogDebug("  code         = 0x{0:x4}", info.Code);
      this.LogDebug("  menu title   = {0}", DvbTextConverter.Convert(info.MenuTitle, info.MenuTitleLength));

      // Receiving application information indicates that the CAM is ready for interaction.
      _isCamPresent = true;
      _isCamReady = true;
      return 0;
    }

    /// <summary>
    /// Invoked by the driver when a CAM wants to close an MMI session.
    /// </summary>
    /// <param name="ciFilter">The CI filter.</param>
    /// <returns>an HRESULT indicating whether the MMI session was successfully closed</returns>
    private int OnCloseMmi(IBaseFilter ciFilter)
    {
      this.LogInfo("SmarDTV USB CI: close MMI call back");
      lock (_caMenuCallBackLock)
      {
        if (_caMenuCallBack != null)
        {
          _caMenuCallBack.OnCiCloseDisplay(0);
        }
        else
        {
          this.LogDebug("SmarDTV USB CI: menu call back not set");
        }
      }
      return 0;
    }

    /// <summary>
    /// Invoked by the driver when an application protocol data unit is received from a CAM.
    /// </summary>
    /// <param name="ciFilter">The CI filter.</param>
    /// <param name="apduLength">The length of the APDU buffer in bytes.</param>
    /// <param name="apdu">A buffer containing the APDU.</param>
    /// <returns>an HRESULT indicating whether the APDU was successfully processed</returns>
    private int OnApdu(IBaseFilter ciFilter, int apduLength, byte[] apdu)
    {
      this.LogInfo("SmarDTV USB CI: APDU call back");
      lock (_caMenuCallBackLock)
      {
        DvbMmiHandler.HandleMmiData(apdu, _caMenuCallBack);
      }
      return 0;
    }

    #endregion

    #region ICustomDevice members

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
      this.LogDebug("SmarDTV USB CI: initialising");

      if (_isSmarDtvUsbCi)
      {
        this.LogWarn("SmarDTV USB CI: extension already initialised");
        return true;
      }

      if (string.IsNullOrEmpty(tunerExternalId))
      {
        this.LogDebug("SmarDTV USB CI: tuner external identifier is not set");
        return false;
      }

      // A machine may only have one instance of each OEM product installed
      // - this is a driver limitation. It is unknown whether a single machine
      // may have multiple instances by connecting instances of different
      // products (we don't explicitly prevent this). The TV Server plugin
      // allows each OEM CI product to be linked to a single tuner. Here we
      // need to know whether this tuner (ie. the one referred to by the
      // external identifier) is currently linked to any of the products.
      ReadOnlyCollection<ISmarDtvUsbCiProduct> productList = SmarDtvUsbCiProductBase.GetProductList();
      foreach (ISmarDtvUsbCiProduct p in productList)
      {
        this.LogDebug("SmarDTV USB CI: {0}...", p.Name);
        if (!tunerExternalId.Equals(p.LinkedTuner))
        {
          this.LogDebug("SmarDTV USB CI:   not linked");
          continue;
        }
        if (p.IsInUse)
        {
          this.LogDebug("SmarDTV USB CI:   already in use");
          continue;
        }
        SmarDtvUsbCiDriverInstallState installState = p.InstallState;
        if (installState != SmarDtvUsbCiDriverInstallState.BdaDriver)
        {
          this.LogDebug("SmarDTV USB CI:   not usable, install state = {0}", installState);
          continue;
        }
        _ciFilter = p.Initialise();
        if (_ciFilter == null)
        {
          this.LogError("SmarDTV USB CI: failed to initialise product {0}", p.Name);
          continue;
        }

        this.LogInfo("SmarDTV USB CI: extension supported");
        _isSmarDtvUsbCi = true;
        _product = p;
        _isFilterInGraph = false;
        return true;
      }

      this.LogDebug("SmarDTV USB CI: tuner not linked to any CI products or otherwise not supported");
      _isSmarDtvUsbCi = false;
      return false;
    }

    #endregion

    #region IDirectShowAddOnDevice member

    /// <summary>
    /// Insert and connect additional filter(s) into the graph.
    /// </summary>
    /// <param name="graph">The tuner filter graph.</param>
    /// <param name="lastFilter">The source filter (usually either a capture/receiver or
    ///   multiplexer filter) to connect the [first] additional filter to.</param>
    /// <returns><c>true</c> if one or more additional filters were successfully added to the graph, otherwise <c>false</c></returns>
    public bool AddToGraph(IFilterGraph2 graph, ref IBaseFilter lastFilter)
    {
      this.LogDebug("SmarDTV USB CI: add to graph");

      if (!_isSmarDtvUsbCi)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return false;
      }
      if (graph == null)
      {
        this.LogError("SmarDTV USB CI: failed to add the filter to the graph, graph is null");
        return false;
      }
      if (lastFilter == null)
      {
        this.LogError("SmarDTV USB CI: failed to add the filter to the graph, last filter is null");
        return false;
      }
      if (_isFilterInGraph)
      {
        this.LogWarn("SmarDTV USB CI: filter already in graph");
        return true;
      }

      bool success = false;
      _graph = graph;
      try
      {
        // Add the CI filter to the graph.
        int hr = _graph.AddFilter(_ciFilter, _product.Name);
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("SmarDTV USB CI: failed to add the filter to the graph, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          return false;
        }
        _isFilterInGraph = true;

        // Connect the filter into the graph.
        IPin tmpOutputPin = DsFindPin.ByDirection(lastFilter, PinDirection.Output, 0);
        IPin tmpInputPin = DsFindPin.ByDirection(_ciFilter, PinDirection.Input, 0);
        try
        {
          if (tmpInputPin == null || tmpOutputPin == null)
          {
            this.LogError("SmarDTV USB CI: failed to locate required pins");
            return false;
          }
          hr = _graph.ConnectDirect(tmpOutputPin, tmpInputPin, null);
        }
        finally
        {
          Release.ComObject("SmarDTV upstream filter output pin", ref tmpOutputPin);
          Release.ComObject("SmarDTV CI filter input pin", ref tmpInputPin);
        }
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("SmarDTV USB CI: failed to connect the filter into the graph, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          return false;
        }

        success = true;
      }
      finally
      {
        if (!success && _isFilterInGraph)
        {
          _isFilterInGraph = false;
          _graph.RemoveFilter(_ciFilter);
        }
      }

      // Success!
      this.LogDebug("SmarDTV USB CI: result = success");
      lastFilter = _ciFilter;
      return true;
    }

    #endregion

    #region ITvServerPlugin members

    /// <summary>
    /// A human-readable name for the extension. This could be a manufacturer or reseller name, or
    /// even a model name and/or number.
    /// </summary>
    public override string Name
    {
      get
      {
        return "SmarDTV USB CI";
      }
    }

    /// <summary>
    /// The version of this TV Server plugin.
    /// </summary>
    public string Version
    {
      get
      {
        return "1.0.0.0";
      }
    }

    /// <summary>
    /// The author of this TV Server plugin.
    /// </summary>
    public string Author
    {
      get
      {
        return "mm1352000";
      }
    }

    /// <summary>
    /// Determine whether this TV Server plugin should only run on the master server, or if it can also
    /// run on slave servers.
    /// </summary>
    /// <remarks>
    /// This property is obsolete. Master-slave configurations are not supported.
    /// </remarks>
    public bool MasterOnly
    {
      get
      {
        return true;
      }
    }

    /// <summary>
    /// Get an instance of the configuration section for use in TV Server configuration (SetupTv).
    /// </summary>
    public SectionSettings Setup
    {
      get
      {
        return new SmarDtvUsbCiConfig();
      }
    }

    /// <summary>
    /// Start this TV Server plugin.
    /// </summary>
    public void Start(IInternalControllerService controllerService)
    {
    }

    /// <summary>
    /// Stop this TV Server plugin.
    /// </summary>
    public void Stop()
    {
    }

    #endregion

    #region ITvServerPluginCommunication members

    /// <summary>
    /// Supply a service class implementation for client-server plugin communication.
    /// </summary>
    public object GetServiceInstance
    {
      get
      {
        return new SmarDtvUsbCiConfigService();
      }
    }

    /// <summary>
    /// Supply a service class interface for client-server plugin communication.
    /// </summary>
    public Type GetServiceInterfaceForContractType
    {
      get
      {
        return typeof(ISmarDtvUsbCiConfigService);
      }
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
      this.LogDebug("SmarDTV USB CI: open conditional access interface");

      if (!_isSmarDtvUsbCi)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return false;
      }
      if (!_isFilterInGraph)
      {
        this.LogDebug("SmarDTV USB CI: filter not added to the BDA filter graph");
        return false;
      }
      if (_isCaInterfaceOpen)
      {
        this.LogWarn("SmarDTV USB CI: conditional access interface is already open");
        return true;
      }

      // Setup call backs and open the interface.
      _ciCallBack = new SmarDtvUsbCiCallBack();
      _ciCallBack.OnApdu = new OnSmarDtvUsbCiApdu(OnApdu);
      _ciCallBack.OnApplicationInfo = new OnSmarDtvUsbCiApplicationInfo(OnApplicationInfo);
      _ciCallBack.OnCiState = new OnSmarDtvUsbCiState(OnCiState);
      _ciCallBack.OnCloseMmi = new OnSmarDtvUsbCiCloseMmi(OnCloseMmi);
      int hr = _product.OpenInterface(ref _ciCallBack);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("SmarDTV USB CI: failed to open conditional access interface, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      SmarDtvUsbCiVersionInfo versionInfo = new SmarDtvUsbCiVersionInfo();
      hr = _product.GetVersionInfo(out versionInfo);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("  plugin version     = {0}", versionInfo.PluginVersion);
        this.LogDebug("  BDA driver version = {0}", versionInfo.BdaVersion);
        this.LogDebug("  USB driver version = {0}", versionInfo.UsbVersion);
        this.LogDebug("  firmware version   = {0}", versionInfo.FirmwareVersion);
        this.LogDebug("  FPGA version       = {0}", versionInfo.FpgaVersion);
      }
      else
      {
        this.LogWarn("SmarDTV USB CI: failed to retrieve version information, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }

      this.LogDebug("SmarDTV USB CI: result = success");
      _isCaInterfaceOpen = true;
      return true;
    }

    /// <summary>
    /// Close the conditional access interface.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully closed, otherwise <c>false</c></returns>
    public bool CloseConditionalAccessInterface()
    {
      this.LogDebug("SmarDTV USB CI: close conditional access interface");

      // I don't know of any way to safely shutdown the interface (stop call
      // backs etc.) except to dispose the filter, which we can't do. It seems
      // that you'll get a BSOD if you invoke USB2CI_Init() with null call back
      // references or a null structure. So the best we can do is not close the
      // interface and warn to the log.
      this.LogWarn("SmarDTV USB CI: not possible to close conditional access interface without destroying graph");
      return false;
    }

    /// <summary>
    /// Reset the conditional access interface.
    /// </summary>
    /// <param name="resetTuner">This parameter will be set to <c>true</c> if the tuner must be reset
    ///   for the interface to be completely and successfully reset.</param>
    /// <returns><c>true</c> if the interface is successfully reset, otherwise <c>false</c></returns>
    public bool ResetConditionalAccessInterface(out bool resetTuner)
    {
      resetTuner = true;
      return true;
    }

    /// <summary>
    /// Determine whether the conditional access interface is ready to receive commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is ready, otherwise <c>false</c></returns>
    public bool IsConditionalAccessInterfaceReady()
    {
      this.LogDebug("SmarDTV USB CI: is conditional access interface ready");
      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return false;
      }

      // The CI/CAM state is automatically updated in the OnCiState() call back.
      this.LogDebug("SmarDTV USB CI: result = {0}", _isCamReady);
      return _isCamReady;
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
      this.LogDebug("SmarDTV USB CI: send conditional access command, list action = {0}, command = {1}", listAction, command);

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return true;
      }
      if (command == CaPmtCommand.OkMmi || command == CaPmtCommand.Query)
      {
        this.LogError("SmarDTV USB CI: conditional access command type {0} is not supported", command);
        return true;
      }
      if (pmt == null)
      {
        this.LogError("SmarDTV USB CI: failed to send conditional access command, PMT not supplied");
        return true;
      }

      // "Not selected" commands do nothing.
      if (command == CaPmtCommand.NotSelected)
      {
        this.LogDebug("SmarDTV USB CI: result = success");
        return true;
      }

      // During development of this class I tried a couple of tricks to get the WinTV-CI to decrypt
      // more than one channel at a time. I came to the conclusion that it is not possible. I tried:
      // - assemble a fake CA PMT structure and pass it to USB2CI_GuiSendPMT
      // - send a CA PMT APDU using USB2CI_APDUToCAM
      // We'll just send this PMT to the CAM regardless of the list management action.
      ReadOnlyCollection<byte> rawPmt = pmt.GetRawPmt();
      byte[] rawPmtCopy = new byte[rawPmt.Count];
      rawPmt.CopyTo(rawPmtCopy, 0);
      int hr = _product.SendPmt(rawPmtCopy, (short)rawPmt.Count);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("SmarDTV USB CI: result = success");
        return true;
      }

      this.LogError("SmarDTV USB CI: failed to send conditional access command, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
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
      lock (_caMenuCallBackLock)
      {
        _caMenuCallBack = callBack;
      }
    }

    /// <summary>
    /// Send a request from the user to the CAM to open the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool EnterMenu()
    {
      this.LogDebug("SmarDTV USB CI: enter menu");

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("SmarDTV USB CI: failed to open menu, the CAM is not ready");
        return false;
      }

      int hr = _product.OpenMmiSession();
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("SmarDTV USB CI: result = success");
        return true;
      }

      this.LogError("SmarDTV USB CI: failed to open menu, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Send a request from the user to the CAM to close the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool CloseMenu()
    {
      this.LogDebug("SmarDTV USB CI: close menu");

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("SmarDTV USB CI: failed to close menu, the CAM is not ready");
        return false;
      }

      byte[] apdu = DvbMmiHandler.CreateMmiClose(0);
      int hr = _product.SendMmiApdu(apdu.Length, apdu);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("SmarDTV USB CI: result = success");
        return true;
      }

      this.LogError("SmarDTV USB CI: failed to close menu, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Send a menu entry selection from the user to the CAM.
    /// </summary>
    /// <param name="choice">The index of the selection as an unsigned byte value.</param>
    /// <returns><c>true</c> if the selection is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool SelectMenuEntry(byte choice)
    {
      this.LogDebug("SmarDTV USB CI: select menu entry, choice = {0}", choice);

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("SmarDTV USB CI: failed to select menu entry, the CAM is not ready");
        return false;
      }

      byte[] apdu = DvbMmiHandler.CreateMmiMenuAnswer(choice);
      int hr = _product.SendMmiApdu(apdu.Length, apdu);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("SmarDTV USB CI: result = success");
        return true;
      }

      this.LogError("SmarDTV USB CI: failed to select menu entry, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
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
      if (answer == null)
      {
        answer = string.Empty;
      }
      this.LogDebug("SmarDTV USB CI: answer enquiry, answer = {0}, cancel = {1}", answer, cancel);

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("SmarDTV USB CI: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("SmarDTV USB CI: failed to answer enquiry, the CAM is not ready");
        return false;
      }

      MmiResponseType responseType = MmiResponseType.Answer;
      if (cancel)
      {
        responseType = MmiResponseType.Cancel;
      }
      byte[] apdu = DvbMmiHandler.CreateMmiEnquiryAnswer(responseType, answer);
      int hr = _product.SendMmiApdu(apdu.Length, apdu);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("SmarDTV USB CI: result = success");
        return true;
      }

      this.LogError("SmarDTV USB CI: failed to answer enquiry, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      if (_product != null)
      {
        if (_graph != null)
        {
          if (_isFilterInGraph)
          {
            _isFilterInGraph = false;
            _graph.RemoveFilter(_ciFilter);
          }
          Release.ComObject("SmarDTV CI graph", ref _graph);
        }
        if (_ciFilter != null)
        {
          _product.Deinitialise();
          _ciFilter = null;
        }
      }

      _isSmarDtvUsbCi = false;
    }

    #endregion
  }
}