#region IMPORTS

using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

#if GAMBIT_NEUROGUIDE
using gambit.neuroguide;
#endif

#if GAMBIT_NEUROGUIDE_RECORDER
using gambit.neuroguide.recorder;
#endif

#endregion

/// <summary>
/// A test script to control and validate the NeuroGuideRecorderManager functionality.
/// Creates a UI for recording, playback, seeking, and deleting sessions.
/// </summary>
public class NeuroGuideRecorderTest: MonoBehaviour
{
    #region PRIVATE - VARIABLES

    // Status and logging
    private string _statusMessage = "Initializing...";
    private Vector2 _scrollPosition;

    // UI Input values
    private string _recordingName = $"Session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.dat";
    private string _seekInput = "0.5";

    // File Management
    private List<string> _availableRecordings = new List<string>();

    #endregion

    #region PUBLIC - START

    /// <summary>
    /// Initializes the NeuroGuide and Recorder managers in sequence.
    /// </summary>
    //--------------------------------//
    void Start()
    //--------------------------------//
    {
        // --- Step 1: Create NeuroGuideManager with debug input enabled ---
        NeuroGuideManager.Options options = new NeuroGuideManager.Options
        {
            enableDebugData = true, // Enables keyboard input (Up/Down arrows)
            showDebugLogs = true
        };

        if(NeuroGuideManager.Instance == null)
        {
            NeuroGuideManager.Create(
                options: options,
                OnSuccess: ( system ) =>
                {
                    _statusMessage = "NeuroGuideManager Ready. Initializing Recorder...";
                    Debug.Log( "TEST: NeuroGuideManager created successfully." );

                    // --- Step 2: Once NG Manager is ready, create the Recorder Manager ---
                    InitializeRecorderManager();
                },
                OnFailed: ( error ) =>
                {
                    _statusMessage = $"ERROR: {error}";
                    Debug.LogError( $"TEST: NeuroGuideManager failed to create: {error}" );
                }
            );
        }
        else
        {
            _statusMessage = "NeuroGuideManager Ready. Initializing Recorder...";
            Debug.Log( "TEST: NeuroGuideManager created successfully." );

            // --- Step 2: Once NeuroGuide Manager is ready, create the Recorder Manager ---
            InitializeRecorderManager();
        }

    }

    #endregion

    #region PUBLIC - ONGUI

    /// <summary>
    /// Draws the debug UI.
    /// </summary>
    //--------------------------------//
    void OnGUI()
    //--------------------------------//
    {
        // Draw a container for all our controls
        GUILayout.Window( 0, new Rect( 10, 10, 400, Screen.height - 20 ), DrawControls, "NeuroGuide Recorder Test" );
    }

    #endregion

    #region PUBLIC - ON DESTROY

    /// <summary>
    /// Ensures managers are properly destroyed when exiting play mode.
    /// </summary>
    //--------------------------------//
    void OnDestroy()
    //--------------------------------//
    {
        // Destroy in reverse order of creation
        if(NeuroGuideRecorderManager.system != null)
        {
            NeuroGuideRecorderManager.Destroy();
        }
        if(NeuroGuideManager.system != null)
        {
            NeuroGuideManager.Destroy();
        }
    }

    #endregion

    #region PRIVATE - INITIALIZE RECORDER MANAGER

    /// <summary>
    /// Creates the NeuroGuideRecorderManager and subscribes to its events.
    /// </summary>
    //--------------------------------//
    private void InitializeRecorderManager()
    //--------------------------------//
    {
        var options = new NeuroGuideRecorderManager.Options
        {
            showDebugLogs = true
        };

        NeuroGuideRecorderManager.Create(
            options: options,
            OnSuccess: ( system ) =>
            {
                _statusMessage = "Systems Ready. Waiting for command.";
                Debug.Log( "TEST: NeuroGuideRecorderManager created successfully." );

                // Subscribe to state changes to update the UI
                system.OnStateUpdate += OnRecorderStateChanged;
                RefreshRecordingsList();
            },
            OnFailed: ( error ) =>
            {
                _statusMessage = $"ERROR: {error}";
                Debug.LogError( $"TEST: NeuroGuideRecorderManager failed to create: {error}" );
            }
        );
    }

    #endregion

    #region UI - DRAWING

    /// <summary>
    /// Contains all the GUILayout logic for the test window.
    /// </summary>
    //--------------------------------//
    private void DrawControls( int windowID )
    //--------------------------------//
    {
        // Do not draw controls if the system isn't ready yet.
        if(NeuroGuideRecorderManager.system == null)
        {
            GUILayout.Label( _statusMessage );
            return;
        }

        var currentState = NeuroGuideRecorderManager.system.state;

        // --- Status Section ---
        GUILayout.Label( $"<b>Status:</b> {currentState}" );
        GUILayout.Label( $"<b>Log:</b> {_statusMessage}" );
        if(currentState == NeuroGuideRecorderManager.State.Playing || currentState == NeuroGuideRecorderManager.State.Paused)
        {
            float progress = NeuroGuideRecorderManager.system.PlaybackProgress;
            GUILayout.Label( $"<b>Playback:</b> {NeuroGuideRecorderManager.system.PlaybackTime:mm\\:ss\\.fff} / {NeuroGuideRecorderManager.system.TotalDuration:mm\\:ss\\.fff}" );
            Rect progressRect = GUILayoutUtility.GetRect( 100, 20 );
            GUI.Box( progressRect, GUIContent.none );
            GUI.Box( new Rect( progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height ), GUIContent.none );
        }

        GUILayout.Space( 10 );

        // --- Recording Section ---
        GUILayout.Label( "<b>1. Recording Controls</b>" );
        GUI.enabled = (currentState == NeuroGuideRecorderManager.State.Idle);
        _recordingName = GUILayout.TextField( _recordingName );
        if(GUILayout.Button( "RECORD" ))
        {
            NeuroGuideRecorderManager.Record( _recordingName );
        }
        GUI.enabled = true;

        GUILayout.Space( 10 );

        // --- Playback Section ---
        GUILayout.Label( "<b>2. Playback Controls</b>" );
        GUI.enabled = (currentState == NeuroGuideRecorderManager.State.Playing);
        if(GUILayout.Button( "PAUSE" ))
            NeuroGuideRecorderManager.Pause();

        GUI.enabled = (currentState == NeuroGuideRecorderManager.State.Paused);
        if(GUILayout.Button( "RESUME" ))
            NeuroGuideRecorderManager.Resume();

        GUI.enabled = (currentState != NeuroGuideRecorderManager.State.Idle);
        if(GUILayout.Button( "STOP" ))
            NeuroGuideRecorderManager.Stop();
        GUI.enabled = true;

        GUILayout.Space( 10 );

        // --- Seeking Section ---
        GUILayout.Label( "<b>3. Seek Controls (While Playing/Paused)</b>" );
        GUI.enabled = (currentState == NeuroGuideRecorderManager.State.Playing || currentState == NeuroGuideRecorderManager.State.Paused);
        _seekInput = GUILayout.TextField( _seekInput );
        GUILayout.BeginHorizontal();
        if(GUILayout.Button( "Seek (0-1)" ))
        {
            if(float.TryParse( _seekInput, out float normalizedTime ))
            {
                NeuroGuideRecorderManager.Seek( normalizedTime );
            }
        }
        if(GUILayout.Button( "Seek (sec)" ))
        {
            if(double.TryParse( _seekInput, out double seconds ))
            {
                NeuroGuideRecorderManager.Seek( TimeSpan.FromSeconds( seconds ) );
            }
        }
        if(GUILayout.Button( "Seek (ID)" ))
        {
            if(int.TryParse( _seekInput, out int id ))
            {
                NeuroGuideRecorderManager.Seek( id );
            }
        }
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUILayout.Space( 10 );

        // --- File Management Section ---
        GUILayout.Label( "<b>4. Saved Recordings</b>" );
        if(GUILayout.Button( "Refresh List" ))
            RefreshRecordingsList();

        _scrollPosition = GUILayout.BeginScrollView( _scrollPosition, false, true );
        GUI.enabled = (currentState == NeuroGuideRecorderManager.State.Idle);

        foreach(string rec in _availableRecordings)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label( Path.GetFileName( rec ) );
            if(GUILayout.Button( "Play", GUILayout.Width( 60 ) ))
            {
                NeuroGuideRecorderManager.Play( Path.GetFileName( rec ) );
            }
            if(GUILayout.Button( "Delete", GUILayout.Width( 60 ) ))
            {
                NeuroGuideRecorderManager.Delete( Path.GetFileName( rec ) );
                RefreshRecordingsList(); // Update UI after deletion
            }
            GUILayout.EndHorizontal();
        }
        GUI.enabled = true;
        GUILayout.EndScrollView();
    }

    #endregion

    #region PRIVATE - ON RECORDER STATE CHANGED

    /// <summary>
    /// Updates the status message when the recorder's state changes.
    /// </summary>
    //---------------------------------------------------------------------------------//
    private void OnRecorderStateChanged( NeuroGuideRecorderManager.State newState )
    //----------------------------------------------------------------------------------//
    {
        _statusMessage = $"State changed to {newState}.";
        if(newState == NeuroGuideRecorderManager.State.Recording)
        {
            // When a new recording starts, create a new default filename for the next one
            _recordingName = $"Session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.dat";
        }

    } //END OnRecorderStateChanged Method

    #endregion

    #region PRIVATE - REFERSH RECORDINGS LIST

    /// <summary>
    /// Scans the recordings directory and updates the list of available files.
    /// </summary>
    //-----------------------------------------//
    private void RefreshRecordingsList()
    //-----------------------------------------//
    {
        if(NeuroGuideRecorderManager.system == null)
        {
            return;
        }

        if(NeuroGuideRecorderManager.system.options == null)
        {
            return;
        }

        var directory = NeuroGuideRecorderManager.system.options.RecordingsDirectory;

        if(Directory.Exists( directory ))
        {
            _availableRecordings = Directory.GetFiles( directory, "*.dat" ).ToList();
        }

    } //END RefershRecordingsList Method

    #endregion

} //END NeuroGuideRecorderTest Class