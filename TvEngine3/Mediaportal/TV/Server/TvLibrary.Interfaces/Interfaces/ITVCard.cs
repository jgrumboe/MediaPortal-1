#region Copyright (C) 2005-2011 Team MediaPortal

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

using System.Collections.Generic;
using Mediaportal.TV.Server.TVLibrary.Interfaces.ChannelLinkage;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Epg;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces
{
  #region event delegates

  /// <summary>
  /// Delegate for the new subchannel event.
  /// </summary>
  /// <param name="subChannelId">The ID of the new subchannel.</param>
  public delegate void OnNewSubChannelDelegate(int subChannelId);

  /// <summary>
  /// Delegate for the after tune event.
  /// </summary>
  public delegate void OnAfterTuneDelegate();

  #endregion

  /// <summary>
  /// interface for a tv card
  /// </summary>
  public interface ITVCard
  {
    #region events

    /// <summary>
    /// Set the tuner's new subchannel event handler.
    /// </summary>
    event OnNewSubChannelDelegate OnNewSubChannelEvent;

    /// <summary>
    /// Set the tuner's after tune event handler.
    /// </summary>
    event OnAfterTuneDelegate OnAfterTuneEvent;

    #endregion

    #region properties

    /// <summary>
    /// Gets or sets the timeout parameters.
    /// </summary>
    /// <value>The parameters.</value>
    ScanParameters Parameters { get; set; }

    /// <summary>
    /// Gets/sets the card name
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Gets/Sets that the card is present
    /// </summary>
    bool CardPresent { get; set; }

    /// <summary>
    /// Get the tuner's unique external identifier.
    /// </summary>		
    string ExternalId { get; }

    /// <summary>
    /// Get the tuner's product instance identifier.
    /// </summary>
    /// <remarks>
    /// The product instance identifier is a shared identifier for all tuner instances derived from a [multi-tuner] product.
    /// </remarks>
    string ProductInstanceId { get; }

    /// <summary>
    /// Get the tuner's instance identifier.
    /// </summary>
    /// <summary>
    /// The tuner instance identifier is a shared identifier for all tuner instances derived from a single physical tuner.
    /// </summary>
    string TunerInstanceId { get; }

    /// <summary>
    /// Method to check if card can tune to the channel specified
    /// </summary>
    /// <returns>true if card can tune to the channel otherwise false</returns>
    bool CanTune(IChannel channel);

    /// <summary>
    /// Stop the card.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets or sets the type of the cam.
    /// </summary>
    /// <value>The type of the cam.</value>
    CamType CamType { get; set; }

    /// <summary>
    /// Gets/sets the card type
    /// </summary>
    CardType CardType { get; }



    /// <summary>
    /// Get the tuner's DiSEqC control interface. This interface is only applicable for satellite tuners.
    /// It is used for controlling switch, positioner and LNB settings.
    /// </summary>
    /// <value><c>null</c> if the tuner is not a satellite tuner or the tuner does not support sending/receiving
    /// DiSEqC commands</value>
    IDiseqcController DiseqcController { get; }

    /// <summary>
    /// Does the tuner support conditional access?
    /// </summary>
    /// <value><c>true</c> if the tuner supports conditional access, otherwise <c>false</c></value>
    bool IsConditionalAccessSupported { get; }

    /// <summary>
    /// Get the device's conditional access menu interaction interface. This interface is only applicable if
    /// conditional access is supported.
    /// </summary>
    /// <value><c>null</c> if the device does not support conditional access</value>
    IConditionalAccessMenuActions CaMenuInterface { get; }

    /// <summary>
    /// Get a count of the number of services that the device is currently decrypting.
    /// </summary>
    /// <value>The number of services currently being decrypted.</value>
    int NumberOfChannelsDecrypting { get; }

    #endregion

    #region Channel linkage handling

    /// <summary>
    /// Starts scanning for linkage info
    /// </summary>
    void StartLinkageScanner(BaseChannelLinkageScanner callBack);

    /// <summary>
    /// Stops/Resets the linkage scanner
    /// </summary>
    void ResetLinkageScanner();

    /// <summary>
    /// Returns the channel linkages grabbed
    /// </summary>
    List<PortalChannel> ChannelLinkages { get; }

    #endregion

    #region epg & scanning

    /// <summary>
    /// Grabs the epg.
    /// </summary>
    /// <param name="callBack">The call back which gets called when epg is received or canceled.</param>
    void GrabEpg(BaseEpgGrabber callBack);

    /// <summary>
    /// Start grabbing the epg while timeshifting
    /// </summary>
    void GrabEpg();

    /// <summary>
    /// Aborts grabbing the epg. This also triggers the OnEpgReceived call back.
    /// </summary>
    void AbortGrabbing();

    /// <summary>
    /// returns a list of all epg data for each channel found.
    /// </summary>
    /// <value>The epg.</value>
    List<EpgChannel> Epg { get; }

    /// <summary>
    /// Get the tuner's channel scanning interface.
    /// </summary>
    ITVScanning ScanningInterface { get; }

    #endregion

    #region tuning & scanning

    /// <summary>
    /// Tune to a specific channel.
    /// </summary>
    /// <param name="subChannelId">The ID of the subchannel associated with the channel that is being tuned.</param>
    /// <param name="channel">The channel to tune to.</param>
    /// <returns>the subchannel associated with the tuned channel</returns>
    ITvSubChannel Tune(int subChannelId, IChannel channel);

    /// <summary>
    /// Scan a specific channel.
    /// </summary>
    /// <param name="subChannelId">The ID of the subchannel associated with the channel that is being scanned.</param>
    /// <param name="channel">The channel to scan.</param>
    /// <returns>the subchannel associated with the scanned channel</returns>
    ITvSubChannel Scan(int subChannelId, IChannel channel);

    /// <summary>
    /// Cancel the current tuning process.
    /// </summary>
    /// <param name="subChannelId">The ID of the subchannel associated with the channel that is being cancelled.</param>
    void CancelTune(int subChannelId);

    #endregion

    /// <summary>
    /// Get/Set the quality
    /// </summary>
    IQuality Quality { get; }

    /// <summary>
    /// Property which returns true if card supports quality control
    /// </summary>
    bool SupportsQualityControl { get; }

    /// <summary>
    /// Reload the tuner's configuration.
    /// </summary>
    void ReloadConfiguration();

    #region properties

    /// <summary>
    /// When the tuner is locked onto a signal this property will return true
    /// otherwise false
    /// </summary>
    bool IsTunerLocked { get; }

    /// <summary>
    /// returns the signal quality
    /// </summary>
    int SignalQuality { get; }

    /// <summary>
    /// returns the signal level
    /// </summary>
    int SignalLevel { get; }

    /// <summary>
    /// Updates the signal state for a card.
    /// </summary>
    void ResetSignalUpdate();

    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    /// <value>The context.</value>
    object Context { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this card is epg grabbing.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is epg grabbing; otherwise, <c>false</c>.
    /// </value>
    bool IsEpgGrabbing { get; set; }

    /// <summary>
    /// Get or set a value indicating whether this tuner is scanning for channels.
    /// </summary>
    /// <value><c>true</c> if the tuner is currently scanning, otherwise <c>false</c></value>
    bool IsScanning { get; set; }

    #endregion

    #region idisposable

    /// <summary>
    /// Disposes this instance.
    /// </summary>
    void Dispose();

    #endregion

    #region sub channels

    /// <summary>
    /// Gets the sub channel.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns></returns>
    ITvSubChannel GetSubChannel(int id);

    /// <summary>
    /// Frees the sub channel.
    /// </summary>
    /// <param name="id">The id.</param>
    void FreeSubChannel(int id);

    /// <summary>
    /// Gets the sub channels.
    /// </summary>
    /// <value>The sub channels.</value>
    ITvSubChannel[] SubChannels { get; }

    #endregion

    /// <summary>
    /// Get the tuning parameters that have been applied to the hardware.
    /// This property returns null when the device is not in use.
    /// </summary>
    IChannel CurrentTuningDetail
    {
      get;
    }
  }
}