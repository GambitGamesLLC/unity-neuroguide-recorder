#region IMPORTS

using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;

#if GAMBIT_NEUROGUIDE
using gambit.neuroguide;
#endif

#if GAMBIT_SINGLETON
using gambit.singleton;
#endif

#endregion

namespace gambit.neuroguide.recorder
{
    /// <summary>
    /// Used to Record, Load, Play, Pause, Resume, Seek, and Delete NeuroGuide data
    /// </summary>
    public class NeuroGuideRecorderManager: Singleton<NeuroGuideRecorderManager>
    {

        #region PUBLIC - VARIABLES

        /// <summary>
        /// Current instance of the NeuroGuideRecorderSystem instantiated during Create()
        /// </summary>
        public static NeuroGuideRecorderSystem system;

        /// <summary>
        /// The base directory where all recording files are stored.
        /// </summary>
        public static string RecordingsDirectory => Path.Combine( Application.persistentDataPath, "NeuroGuideRecordings" );

        #endregion

        #region PRIVATE - VARIABLES

        /// <summary>
        /// Handles writing data in a binary format, which is highly efficient for storage.
        /// This is initialized when a new recording starts and writes data to the _fileStream.
        /// </summary>
        private BinaryWriter _writer;

        /// <summary>
        /// Represents the direct connection to the recording file on the disk.
        /// It's opened when recording begins and closed when it's finished, ensuring data is saved correctly.
        /// </summary>
        private FileStream _fileStream;

        /// <summary>
        /// Stores the full, absolute file path for the session that is currently being recorded or played back.
        /// This is crucial for managing the active file, especially for operations like Stop() or Delete().
        /// </summary>
        private string _activeRecordingPath;

        /// <summary>
        /// Acts as a simple counter during a recording session. It tracks the number of data points
        /// saved and assigns a unique ID to each RecordedDataPoint.
        /// </summary>
        private int _recordedDataCount;

        /// <summary>
        /// A list that holds the entire recorded session in memory after it's loaded from a file.
        /// Loading the data into this list allows for fast, responsive playback and seeking without constant disk access.
        /// </summary>
        private List<RecordedDataPoint> _loadedRecording;

        /// <summary>
        /// A reference to the active playback coroutine. Storing this reference is essential
        /// to be able to stop or interrupt the playback process for actions like Pause(), Seek(), or Stop().
        /// </summary>
        private Coroutine _playbackCoroutine;

        /// <summary>
        /// Acts as a "cursor" or pointer to the current position within the _loadedRecording list during playback.
        /// It determines which data point is next to be sent to the NeuroGuideManager.
        /// </summary>
        private int _playbackIndex = 0;

        #endregion

        #region PUBLIC - ON DESTROY

        /// <summary>
        /// WHen this object is destroyed, clean up any threads, cororoutines, and listeners
        /// </summary>
        //--------------------------------//
        protected override void OnDestroy()
        //--------------------------------//
        {
            Stop();
            base.OnDestroy();

        } //END OnDestroy Method

        #endregion

        #region PUBLIC - CREATION OPTIONS

        /// <summary>
        /// Options object you can pass in to customize the spawned NeuroGuideRecorder system
        /// </summary>
        //---------------------------------------------//
        public class Options
        //---------------------------------------------//
        {
            /// <summary>
            /// Should debug logs be printed to the console log?
            /// </summary>
            public bool showDebugLogs = true;

        } //END Options

        #endregion

        #region PUBLIC - ENUM - STATE

        /// <summary>
        /// The state enum of the NeuroGuideRecorder system
        /// </summary>
        public enum State
        {
            Idle,
            Recording,
            Playing,
            Paused
        }

        #endregion

        #region PUBLIC - RECORDED DATA POINT STRUCT

        /// <summary>
        /// A struct for storing a single data point to a file.
        /// Uses efficient data types for storage.
        /// </summary>
        [System.Serializable]
        public struct RecordedDataPoint
        {
            //Unique identifier for this data point in the sequence.
            public int id;

            //Timestamp stored as Ticks for precision and efficiency.
            public long timestampTicks;

            //Reward state stored as a single byte (0 or 1).
            public byte rewardState;
        }

        #endregion

        #region PUBLIC - RETURN CLASS : NEUROGUIDERECORDER SYSTEM

        /// <summary>
        /// NeuroGuideRecorder System generated when Create() is successfully called. 
        /// Contains values important for future modification and communication with the NeuroGuideRecorder Manager
        /// </summary>
        //-----------------------------------------//
        public class NeuroGuideRecorderSystem
        //-----------------------------------------//
        {
            /// <summary>
            /// The options passed in during Create()
            /// </summary>
            public Options options = new Options();

            /// <summary>
            /// The current state of the NeuroGuideRecorder
            /// </summary>
            public State state = State.Idle;

            /// <summary>
            /// Gets the current playback progress as a normalized value (0.0 to 1.0).
            /// </summary>
            public float PlaybackProgress { get; internal set; } = 0f;

            /// <summary>
            /// Gets the current elapsed time of the playback.
            /// </summary>
            public TimeSpan PlaybackTime { get; internal set; } = TimeSpan.Zero;

            /// <summary>
            /// Gets the total duration of the currently loaded recording.
            /// </summary>
            public TimeSpan TotalDuration { get; internal set; } = TimeSpan.Zero;

            /// <summary>
            /// Unity action to call when the recorder state has changed
            /// </summary>
            public Action<State> OnStateUpdate;

        } //END NeuroGuideRecorderSystem Class

        #endregion

        #region PUBLIC - CREATE

        /// <summary>
        /// Creates a NeuroGuideRecorderSystem and sets it up to prepare for 
        /// accessing and creating new NeuroGuide recordings
        /// </summary>
        /// <param name="options">Options object that determines how the NeuroGuideRecorderSystem is initialized</param>
        /// <param name="OnSuccess">Callback action when the NeuroGuideRecorder system successfully initializes</param>
        /// <param name="OnFailed">Callback action that returns a string with an error message when initialization fails</param>
        //-------------------------------------//
        public static void Create(
            Options options = null,
            Action<NeuroGuideRecorderSystem> OnSuccess = null,
            Action<string> OnFailed = null )
        //-------------------------------------//
        {
            if(system != null)
            {
                OnFailed?.Invoke( "NeuroGuideRecorderManager.cs Create() NeuroGuideRecorderSystem object already exists. Unable to continue." );
                return;
            }

#if GAMBIT_NEUROGUIDE
            if(NeuroGuideManager.system == null)
            {
                OnFailed?.Invoke( "NeuroGuideRecorderManager.cs Create() NeuroGuideManager has not been initialized. Unable to continue." );
                return;
            }
#endif

            //If the user didn't pass in any options, use the defaults
            if(options == null)
                options = new Options();

            //Generate a NeuroGuideSystem object
            system = new NeuroGuideRecorderSystem();
            system.options = options;

            // Ensure the directory for recordings exists.
            if(!Directory.Exists( RecordingsDirectory ))
            {
                Directory.CreateDirectory( RecordingsDirectory );
            }

            //Mark the system as idle, so Update methods know we're ready
            system.state = State.Idle;
            SendStateUpdatedMessage();

            //If we were unable to make a connection to the NeuroGuide hardware, we cannot continue
            if(system.state == State.Idle)
            {
                OnFailed?.Invoke( "NeuroGuideManager.cs Create() Unable to connect to NeuroGuide hardware. Unable to continue." );
                return;
            }

            //Access a variable of the singleton instance, this will ensure it is initialized in the hierarchy with a GameObject representation
            //Doing this makes sure that Unity Lifecycle methods like Update() will run
            Instance.enabled = true;

            //We're done, call the OnSuccess callback
            OnSuccess?.Invoke( system );

        } //END Create Method

        #endregion

        #region PUBLIC - DESTROY

        /// <summary>
        /// Stops listening to input and prepare the manager to have Create() called again
        /// </summary>
        //-------------------------------//
        public static void Destroy()
        //-------------------------------//
        {

            if(system == null)
            {
                return;
            }

            Instance.Invoke( "FinishDestroy", .1f );

        } //END Destroy Method

        /// <summary>
        /// Invoked by Destroy(), after allowing for tweens to be cleaned up, destroys the gameobjects
        /// </summary>
        //------------------------------------//
        private void FinishDestroy()
        //------------------------------------//
        {
            if(system.options.showDebugLogs)
            {
                Debug.Log( "NeuroGuideRecorderManager.cs FinishDestroy() cleaned up objects and data, ready to Create()" );
            }

            system = null;

        } //END FinishDestroy

        #endregion
        
        #region PUBLIC - RECORD

        /// <summary>
        /// Starts recording NeuroGuide data to a specified file.
        /// </summary>
        /// <param name="recordingName">The name of the file to save (e.g., "Session1.dat").</param>
        //---------------------------------------//
        public static void Record( string recordingName )
        //---------------------------------------//
        {
            if(system == null)
                return;
            if(system.state != State.Idle)
            {
                if(system.options.showDebugLogs)
                    Debug.LogWarning( "Cannot start recording. Recorder is currently busy." );
                return;
            }
            Instance.RecordInternal( recordingName );
        }

        #endregion

        #region PUBLIC - PLAY

        /// <summary>
        /// Starts playing a recorded NeuroGuide session from a file.
        /// </summary>
        /// <param name="recordingName">The name of the recording file to play.</param>
        //------------------------------------//
        public static void Play( string recordingName )
        //------------------------------------//
        {
            if(system == null)
                return;
            if(system.state != State.Idle)
            {
                if(system.options.showDebugLogs)
                    Debug.LogWarning( "Cannot start playback. Recorder is currently busy." );
                return;
            }
            Instance.PlayInternal( recordingName );
        }

        #endregion

        #region PUBLIC - PAUSE

        /// <summary>
        /// Pauses the current playback.
        /// </summary>
        //-------------------------------//
        public static void Pause()
        //-------------------------------//
        {
            if(system == null || system.state != State.Playing)
                return;
            Instance.UpdateState( State.Paused );
        }

        #endregion

        #region PUBLIC - RESUME

        /// <summary>
        /// Resumes a paused playback.
        /// </summary>
        //---------------------------------//
        public static void Resume()
        //---------------------------------//
        {
            if(system == null || system.state != State.Paused)
                return;
            Instance.UpdateState( State.Playing );
        }

        #endregion

        #region PUBLIC - STOP

        /// <summary>
        /// Stops the current operation (Recording or Playback) and returns to Idle.
        /// </summary>
        //------------------------------//
        public static void Stop()
        //------------------------------//
        {
            if(system == null || system.state == State.Idle)
                return;
            NeuroGuideRecorderManager.Stop();
        }

        #endregion

        #region PUBLIC - SEEK

        /// <summary>
        /// Jumps to a specific time in the recording based on a normalized value.
        /// </summary>
        /// <param name="normalizedTime">Time to seek to, from 0.0 (start) to 1.0 (end).</param>
        //---------------------------------------//
        public static void Seek( float normalizedTime )
        //---------------------------------------//
        {
            if(system == null || (system.state != State.Playing && system.state != State.Paused))
                return;
            Instance.SeekInternal( normalizedTime );
        }

        /// <summary>
        /// Jumps to an exact time in the recording.
        /// </summary>
        /// <param name="time">The exact time from the beginning of the recording.</param>
        //---------------------------------------//
        public static void Seek( TimeSpan time )
        //---------------------------------------//
        {
            if(system == null || (system.state != State.Playing && system.state != State.Paused))
                return;
            Instance.SeekInternal( time );
        }

        /// <summary>
        /// Jumps to a specific data point by its index (ID).
        /// </summary>
        /// <param name="dataIndex">The zero-based index of the data point to seek to.</param>
        //---------------------------------------//
        public static void Seek( int dataIndex )
        //---------------------------------------//
        {
            if(system == null || (system.state != State.Playing && system.state != State.Paused))
                return;
            Instance.SeekInternal( dataIndex );
        }

        #endregion

        #region PUBLIC - DELETE

        /// <summary>
        /// Deletes a recording from local storage.
        /// </summary>
        /// <param name="recordingName">The name of the recording file to delete.</param>
        //---------------------------------------//
        public static void Delete( string recordingName )
        //---------------------------------------//
        {
            if(system == null)
                return;
            Instance.DeleteInternal( recordingName );
        }

        #endregion

        #region PUBLIC - ON APPLICATION QUIT

        /// <summary>
        /// Unity lifecycle method, used to clean up threads and unmanagement memory when application quits
        /// </summary>
        //---------------------------------------------------//
        protected override void OnApplicationQuit()
        //---------------------------------------------------//
        {
            Destroy();
            base.OnApplicationQuit();

        } //END OnApplicationQuit Method

        #endregion

        #region PRIVATE - RECORD INTERNAL

        //---------------------------------------//
        private void RecordInternal( string recordingName )
        //---------------------------------------//
        {
            try
            {
                _activeRecordingPath = Path.Combine( RecordingsDirectory, recordingName );
                _fileStream = new FileStream( _activeRecordingPath, FileMode.Create, FileAccess.Write );
                _writer = new BinaryWriter( _fileStream );
                _recordedDataCount = 0;

#if GAMBIT_NEUROGUIDE
                // Subscribe to the data feed from the main manager
                NeuroGuideManager.system.OnDataUpdate += OnDataReceivedForRecording;
#endif

                UpdateState( State.Recording );
                if(system.options.showDebugLogs)
                    Debug.Log( $"Recording started: {_activeRecordingPath}" );
            }
            catch(Exception e)
            {
                Debug.LogError( $"Failed to start recording: {e.Message}" );
                CleanupRecordingResources();
            }

        } //END RecordInternal Method

        #endregion

        #region PRIVATE - PLAY INTERNAL

        //------------------------------------//
        private void PlayInternal( string recordingName )
        //------------------------------------//
        {
            string path = Path.Combine( RecordingsDirectory, recordingName );
            if(!File.Exists( path ))
            {
                Debug.LogError( $"Playback failed. File not found: {path}" );
                return;
            }

            if(!LoadRecordingFromFile( path ))
                return;

            UpdateState( State.Playing );
            _playbackIndex = 0;
            _playbackCoroutine = StartCoroutine( PlaybackCoroutine() );
        }

        #endregion

        #region PRIVATE - STOP INTERNAL

        //------------------------------//
        private void StopInternal()
        //------------------------------//
        {
            if(system.state == State.Recording)
            {
                StopRecordingInternal();
            }
            else if(system.state == State.Playing || system.state == State.Paused)
            {
                StopPlaybackInternal();
            }
        }

        #endregion

        #region PRIVATE - SEEK INTERNAL

        //---------------------------------------//
        private void SeekInternal( float normalizedTime )
        //---------------------------------------//
        {
            if(system.TotalDuration.Ticks == 0)
                return;
            long targetTicks = (long)(system.TotalDuration.Ticks * Mathf.Clamp01( normalizedTime ));
            SeekInternal( TimeSpan.FromTicks( targetTicks ) );
        }

        //---------------------------------------//
        private void SeekInternal( TimeSpan time )
        //---------------------------------------//
        {
            if(_loadedRecording == null || _loadedRecording.Count == 0)
                return;

            long recordingStartTicks = _loadedRecording[ 0 ].timestampTicks;
            long targetAbsoluteTicks = recordingStartTicks + time.Ticks;

            int closestIndex = 0;
            long smallestDiff = long.MaxValue;
            for(int i = 0; i < _loadedRecording.Count; i++)
            {
                long diff = Math.Abs( _loadedRecording[ i ].timestampTicks - targetAbsoluteTicks );
                if(diff < smallestDiff)
                {
                    smallestDiff = diff;
                    closestIndex = i;
                }
            }
            SeekInternal( closestIndex );
        }

        //---------------------------------------//
        private void SeekInternal( int dataIndex )
        //---------------------------------------//
        {
            if(_loadedRecording == null || _loadedRecording.Count == 0)
                return;
            _playbackIndex = Mathf.Clamp( dataIndex, 0, _loadedRecording.Count - 1 );

            if(_playbackCoroutine != null)
                StopCoroutine( _playbackCoroutine );

            if(system.state == State.Playing)
            {
                _playbackCoroutine = StartCoroutine( PlaybackCoroutine() );
            }
            else
            {
                UpdatePlaybackProgress();
            }
        }

        #endregion

        #region PRIVATE - DELETE INTERNAL

        //---------------------------------------//
        private void DeleteInternal( string recordingName )
        //---------------------------------------//
        {
            string path = Path.Combine( RecordingsDirectory, recordingName );

            if(system.state != State.Idle && _activeRecordingPath == path)
            {
                StopInternal();
            }

            if(!File.Exists( path ))
            {
                if(system.options.showDebugLogs)
                    Debug.LogWarning( $"File to delete not found: {path}" );
                return;
            }

            try
            {
                File.Delete( path );
                if(system.options.showDebugLogs)
                    Debug.Log( $"Deleted recording: {path}" );
            }
            catch(Exception e)
            {
                Debug.LogError( $"Error deleting file: {e.Message}" );
            }
        }

        #endregion

        #region PRIVATE - PLAYBACK COROUTINE

        /// <summary>
        /// Coroutine that handles the real-time playback of recorded data.
        /// </summary>
        //---------------------------------------//
        private IEnumerator PlaybackCoroutine()
        //---------------------------------------//
        {
            if(_loadedRecording.Count == 0)
            {
                StopPlaybackInternal();
                yield break;
            }

            _playbackIndex = Mathf.Clamp( _playbackIndex, 0, _loadedRecording.Count - 1 );

            while(_playbackIndex < _loadedRecording.Count)
            {
                while(system.state == State.Paused)
                {
                    yield return null;
                }

                if(system.state != State.Playing)
                {
                    yield break;
                }

                var currentPoint = _loadedRecording[ _playbackIndex ];
                SendUDPData( currentPoint.rewardState );
                UpdatePlaybackProgress();

                if(_playbackIndex < _loadedRecording.Count - 1)
                {
                    var nextPoint = _loadedRecording[ _playbackIndex + 1 ];
                    long delayTicks = nextPoint.timestampTicks - currentPoint.timestampTicks;
                    yield return new WaitForSecondsRealtime( (float)delayTicks / TimeSpan.TicksPerSecond );
                }

                _playbackIndex++;
            }

            if(system.options.showDebugLogs)
                Debug.Log( "Playback finished." );
            StopPlaybackInternal();
        }

        #endregion

        #region PRIVATE - ON DATA RECIEVED FOR RECORDING

        /// <summary>
        /// Callback handler for writing data to a file during recording.
        /// </summary>
        //---------------------------------------------------------//
        private void OnDataReceivedForRecording( NeuroGuideData? data )
        //---------------------------------------------------------//
        {
            if(_writer == null || !data.HasValue)
                return;

            var point = new RecordedDataPoint
            {
                id = _recordedDataCount++,
                timestampTicks = data.Value.timestamp.Ticks,
                rewardState = (byte)(data.Value.isRecievingReward ? 1 : 0)
            };

            _writer.Write( point.id );
            _writer.Write( point.timestampTicks );
            _writer.Write( point.rewardState );
        }

        #endregion

        #region PRIVATE - LOAD RECORDING FROM FILE

        /// <summary>
        /// Loads a recording file into memory.
        /// </summary>
        /// <returns>True if loading was successful, otherwise false.</returns>
        //---------------------------------------------------------//
        private bool LoadRecordingFromFile( string path )
        //---------------------------------------------------------//
        {
            _loadedRecording = new List<RecordedDataPoint>();
            _activeRecordingPath = path;

            try
            {
                using(var stream = new FileStream( path, FileMode.Open, FileAccess.Read ))
                using(var reader = new BinaryReader( stream ))
                {
                    while(reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        var point = new RecordedDataPoint
                        {
                            id = reader.ReadInt32(),
                            timestampTicks = reader.ReadInt64(),
                            rewardState = reader.ReadByte()
                        };
                        _loadedRecording.Add( point );
                    }
                }

                if(_loadedRecording.Count > 0)
                {
                    long startTicks = _loadedRecording[ 0 ].timestampTicks;
                    long endTicks = _loadedRecording[ _loadedRecording.Count - 1 ].timestampTicks;
                    system.TotalDuration = TimeSpan.FromTicks( endTicks - startTicks );
                }
                else
                {
                    system.TotalDuration = TimeSpan.Zero;
                }

                if(system.options.showDebugLogs)
                    Debug.Log( $"Loaded {_loadedRecording.Count} data points from {path}. Total duration: {system.TotalDuration}." );
                return true;
            }
            catch(Exception e)
            {
                Debug.LogError( $"Failed to load recording file: {e.Message}" );
                _loadedRecording = null;
                system.TotalDuration = TimeSpan.Zero;
                return false;
            }
        }

        #endregion

        #region PRIVATE - UPDATE PLAYBACK PROGRESS

        /// <summary>Updates the public progress properties based on the current playback index.</summary>
        //---------------------------------------------//
        private void UpdatePlaybackProgress()
        //---------------------------------------------//
        {
            if(_loadedRecording == null || _loadedRecording.Count == 0)
                return;
            long startTicks = _loadedRecording[ 0 ].timestampTicks;
            long currentTicks = _loadedRecording[ _playbackIndex ].timestampTicks;
            system.PlaybackTime = TimeSpan.FromTicks( currentTicks - startTicks );
            system.PlaybackProgress = system.TotalDuration.Ticks > 0 ? (float)system.PlaybackTime.Ticks / system.TotalDuration.Ticks : 0;
        }

        #endregion

        #region PRIVATE - CLEANUP - STOP RECORDING INTERNAL

        /// <summary>Stops an active recording and cleans up resources.</summary>
        //----------------------------------------------------//
        private void StopRecordingInternal()
        //----------------------------------------------------//
        {
#if GAMBIT_NEUROGUIDE
            if(NeuroGuideManager.system != null)
            {
                NeuroGuideManager.system.OnDataUpdate -= OnDataReceivedForRecording;
            }
#endif
            CleanupRecordingResources();
            UpdateState( State.Idle );
            if(system.options.showDebugLogs)
                Debug.Log( $"Recording stopped. {_recordedDataCount} data points saved to {_activeRecordingPath}." );
        }

        #endregion

        #region PRIVATE - CLEANUP - STOP PLAYBACK INTERNAL

        /// <summary>Stops active playback and cleans up resources.</summary>
        //----------------------------------------------------//
        private void StopPlaybackInternal()
        //----------------------------------------------------//
        {
            if(_playbackCoroutine != null)
            {
                StopCoroutine( _playbackCoroutine );
                _playbackCoroutine = null;
            }

            _loadedRecording = null;
            _activeRecordingPath = null;
            _playbackIndex = 0;
            system.PlaybackProgress = 0f;
            system.PlaybackTime = TimeSpan.Zero;
            system.TotalDuration = TimeSpan.Zero;

            UpdateState( State.Idle );
        }

        #endregion

        #region PRIVATE - CLEANUP - CLEANUP RECORDING RESOURCES

        /// <summary>Safely closes the file stream and writer for recordings.</summary>
        //----------------------------------------------------//
        private void CleanupRecordingResources()
        //----------------------------------------------------//
        {
            _writer?.Close();
            _fileStream?.Close();
            _writer = null;
            _fileStream = null;
        }

        #endregion

        #region PRIVATE - SEND UDP DATA

        /// <summary>
        /// Sends a byte to be picked up by the NeuroGuideManager
        /// </summary>
        /// <param name="data"></param>
        //----------------------------------------------//
        private void SendUDPData( byte data )
        //----------------------------------------------//
        {
#if GAMBIT_NEUROGUIDE
            if(NeuroGuideManager.Instance == null)
            {
                if(system.options.showDebugLogs)
                    Debug.LogError( "NeuroGuideRecorderManager.cs SendUDPData() NeuroGuideManager.Instance is null. Unable to continue" );
                return;
            }
            NeuroGuideManager.Instance.SendUDPData( data );
#else
            if (system.options.showDebugLogs) Debug.LogWarning("NeuroGuideRecorderManager.cs SendUDPData() called but GAMBIT_NEUROGUIDE is not defined.");
#endif
        } //END SendUDPData Method

        #endregion

        #region PRIVATE - SEND STATE CHANGED MESSAGE

        /// <summary>Updates the current state and invokes the OnStateUpdate action.</summary>
        //-----------------------------------------------//
        private void UpdateState( State newState )
        //-----------------------------------------------//
        {
            if(system == null || system.state == newState)
                return;
            system.state = newState;
            SendStateUpdatedMessage();
        }

        /// <summary>
        /// Sends a message out to any listeners via the Unity Action<> system
        /// </summary>
        //---------------------------------------------------//
        private static void SendStateUpdatedMessage()
        //---------------------------------------------------//
        {
            if(system == null)
            {
                return;
            }

            if(system.options.showDebugLogs)
                Debug.Log( "NeuroGuideRecorderManager.cs SendStateUpdatedMessage() state = " + system.state.ToString() );
            system.OnStateUpdate?.Invoke( system.state );

        } //END SendStateUpdatedMessage Method

        #endregion

    } //END NeuroGuideRecorderManager Class

} //END gambit.neuroguide.recorder Namespace