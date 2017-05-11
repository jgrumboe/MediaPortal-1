//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.Serialization;

namespace Mediaportal.TV.Server.TVDatabase.Entities
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(Tuner))]
    public partial class StreamTunerSettings: IObjectWithChangeTracker, INotifyPropertyChanged
    {
        #region Primitive Properties
    
        [DataMember]
        public int IdStreamTunerSettings
        {
            get { return _idStreamTunerSettings; }
            set
            {
                if (_idStreamTunerSettings != value)
                {
                    if (ChangeTracker.ChangeTrackingEnabled && ChangeTracker.State != ObjectState.Added)
                    {
                        throw new InvalidOperationException("The property 'IdStreamTunerSettings' is part of the object's key and cannot be changed. Changes to key properties can only be made when the object is not being tracked or is in the Added state.");
                    }
                    if (!IsDeserializing)
                    {
                        if (Tuner != null && Tuner.IdTuner != value)
                        {
                            Tuner = null;
                        }
                    }
                    _idStreamTunerSettings = value;
                    OnPropertyChanged("IdStreamTunerSettings");
                }
            }
        }
        private int _idStreamTunerSettings;
    
        [DataMember]
        public int ReceiveDataTimeLimit
        {
            get { return _receiveDataTimeLimit; }
            set
            {
                if (_receiveDataTimeLimit != value)
                {
                    _receiveDataTimeLimit = value;
                    OnPropertyChanged("ReceiveDataTimeLimit");
                }
            }
        }
        private int _receiveDataTimeLimit;
    
        [DataMember]
        public int BufferSize
        {
            get { return _bufferSize; }
            set
            {
                if (_bufferSize != value)
                {
                    _bufferSize = value;
                    OnPropertyChanged("BufferSize");
                }
            }
        }
        private int _bufferSize;
    
        [DataMember]
        public int BufferSizeMaximum
        {
            get { return _bufferSizeMaximum; }
            set
            {
                if (_bufferSizeMaximum != value)
                {
                    _bufferSizeMaximum = value;
                    OnPropertyChanged("BufferSizeMaximum");
                }
            }
        }
        private int _bufferSizeMaximum;
    
        [DataMember]
        public int OpenConnectionAttemptLimit
        {
            get { return _openConnectionAttemptLimit; }
            set
            {
                if (_openConnectionAttemptLimit != value)
                {
                    _openConnectionAttemptLimit = value;
                    OnPropertyChanged("OpenConnectionAttemptLimit");
                }
            }
        }
        private int _openConnectionAttemptLimit;
    
        [DataMember]
        public bool DumpInput
        {
            get { return _dumpInput; }
            set
            {
                if (_dumpInput != value)
                {
                    _dumpInput = value;
                    OnPropertyChanged("DumpInput");
                }
            }
        }
        private bool _dumpInput;
    
        [DataMember]
        public int RtspOpenConnectionTimeLimit
        {
            get { return _rtspOpenConnectionTimeLimit; }
            set
            {
                if (_rtspOpenConnectionTimeLimit != value)
                {
                    _rtspOpenConnectionTimeLimit = value;
                    OnPropertyChanged("RtspOpenConnectionTimeLimit");
                }
            }
        }
        private int _rtspOpenConnectionTimeLimit;
    
        [DataMember]
        public bool RtspSendCommandOptions
        {
            get { return _rtspSendCommandOptions; }
            set
            {
                if (_rtspSendCommandOptions != value)
                {
                    _rtspSendCommandOptions = value;
                    OnPropertyChanged("RtspSendCommandOptions");
                }
            }
        }
        private bool _rtspSendCommandOptions;
    
        [DataMember]
        public bool RtspSendCommandDescribe
        {
            get { return _rtspSendCommandDescribe; }
            set
            {
                if (_rtspSendCommandDescribe != value)
                {
                    _rtspSendCommandDescribe = value;
                    OnPropertyChanged("RtspSendCommandDescribe");
                }
            }
        }
        private bool _rtspSendCommandDescribe;
    
        [DataMember]
        public bool RtspKeepAliveWithOptions
        {
            get { return _rtspKeepAliveWithOptions; }
            set
            {
                if (_rtspKeepAliveWithOptions != value)
                {
                    _rtspKeepAliveWithOptions = value;
                    OnPropertyChanged("RtspKeepAliveWithOptions");
                }
            }
        }
        private bool _rtspKeepAliveWithOptions;
    
        [DataMember]
        public string NetworkInterface
        {
            get { return _networkInterface; }
            set
            {
                if (_networkInterface != value)
                {
                    _networkInterface = value;
                    OnPropertyChanged("NetworkInterface");
                }
            }
        }
        private string _networkInterface;
    
        [DataMember]
        public int FileRepeatCount
        {
            get { return _fileRepeatCount; }
            set
            {
                if (_fileRepeatCount != value)
                {
                    _fileRepeatCount = value;
                    OnPropertyChanged("FileRepeatCount");
                }
            }
        }
        private int _fileRepeatCount;
    
        [DataMember]
        public int RtpSwitchToUdpPacketCount
        {
            get { return _rtpSwitchToUdpPacketCount; }
            set
            {
                if (_rtpSwitchToUdpPacketCount != value)
                {
                    _rtpSwitchToUdpPacketCount = value;
                    OnPropertyChanged("RtpSwitchToUdpPacketCount");
                }
            }
        }
        private int _rtpSwitchToUdpPacketCount;

        #endregion
        #region Navigation Properties
    
        [DataMember]
        public Tuner Tuner
        {
            get { return _tuner; }
            set
            {
                if (!ReferenceEquals(_tuner, value))
                {
                    if (ChangeTracker.ChangeTrackingEnabled && ChangeTracker.State != ObjectState.Added && value != null)
                    {
                        // This the dependent end of an identifying relationship, so the principal end cannot be changed if it is already set,
                        // otherwise it can only be set to an entity with a primary key that is the same value as the dependent's foreign key.
                        if (IdStreamTunerSettings != value.IdTuner)
                        {
                            throw new InvalidOperationException("The principal end of an identifying relationship can only be changed when the dependent end is in the Added state.");
                        }
                    }
                    var previousValue = _tuner;
                    _tuner = value;
                    FixupTuner(previousValue);
                    OnNavigationPropertyChanged("Tuner");
                }
            }
        }
        private Tuner _tuner;

        #endregion
        #region ChangeTracking
    
        protected virtual void OnPropertyChanged(String propertyName)
        {
            if (ChangeTracker.State != ObjectState.Added && ChangeTracker.State != ObjectState.Deleted)
            {
                ChangeTracker.State = ObjectState.Modified;
            }
            if (_propertyChanged != null)
            {
                _propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    
        protected virtual void OnNavigationPropertyChanged(String propertyName)
        {
            if (_propertyChanged != null)
            {
                _propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    
        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged{ add { _propertyChanged += value; } remove { _propertyChanged -= value; } }
        private event PropertyChangedEventHandler _propertyChanged;
        private ObjectChangeTracker _changeTracker;
    
        [DataMember]
        public ObjectChangeTracker ChangeTracker
        {
            get
            {
                if (_changeTracker == null)
                {
                    _changeTracker = new ObjectChangeTracker();
                    _changeTracker.ObjectStateChanging += HandleObjectStateChanging;
                }
                return _changeTracker;
            }
            set
            {
                if(_changeTracker != null)
                {
                    _changeTracker.ObjectStateChanging -= HandleObjectStateChanging;
                }
                _changeTracker = value;
                if(_changeTracker != null)
                {
                    _changeTracker.ObjectStateChanging += HandleObjectStateChanging;
                }
            }
        }
    
        private void HandleObjectStateChanging(object sender, ObjectStateChangingEventArgs e)
        {
            if (e.NewState == ObjectState.Deleted)
            {
                ClearNavigationProperties();
            }
        }
    
        protected bool IsDeserializing { get; private set; }
    
        [OnDeserializing]
        public void OnDeserializingMethod(StreamingContext context)
        {
            IsDeserializing = true;
        }
    
        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context)
        {
            IsDeserializing = false;
            ChangeTracker.ChangeTrackingEnabled = true;
        }
    
        // This entity type is the dependent end in at least one association that performs cascade deletes.
        // This event handler will process notifications that occur when the principal end is deleted.
        internal void HandleCascadeDelete(object sender, ObjectStateChangingEventArgs e)
        {
            if (e.NewState == ObjectState.Deleted)
            {
                this.MarkAsDeleted();
            }
        }
    
        protected virtual void ClearNavigationProperties()
        {
            Tuner = null;
        }

        #endregion
        #region Association Fixup
    
        private void FixupTuner(Tuner previousValue)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (previousValue != null && ReferenceEquals(previousValue.StreamTunerSettings, this))
            {
                previousValue.StreamTunerSettings = null;
            }
    
            if (Tuner != null)
            {
                Tuner.StreamTunerSettings = this;
                IdStreamTunerSettings = Tuner.IdTuner;
            }
    
            if (ChangeTracker.ChangeTrackingEnabled)
            {
                if (ChangeTracker.OriginalValues.ContainsKey("Tuner")
                    && (ChangeTracker.OriginalValues["Tuner"] == Tuner))
                {
                    ChangeTracker.OriginalValues.Remove("Tuner");
                }
                else
                {
                    ChangeTracker.RecordOriginalValue("Tuner", previousValue);
                }
                if (Tuner != null && !Tuner.ChangeTracker.ChangeTrackingEnabled)
                {
                    Tuner.StartTracking();
                }
            }
        }

        #endregion
    }
}
