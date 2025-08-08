#region IMPORTS
using UnityEngine;
using System;
using static gambit.neuroguide.NeuroGuideManager;

using UnityEngine.Rendering.VirtualTexturing;
using System.Net;
using System.Net.Sockets;





#if GAMBIT_SINGLETON
using gambit.singleton;
#endif

#endregion

namespace gambit.neuroguide.recorder
{
    /// <summary>
    /// Used to Record, Load, Play, Pause, Resume, Seekd, and Delete NeuroGuide data
    /// </summary>
    public class NeuroGuideRecorderManager: Singleton<NeuroGuideRecorderManager>
    {

        #region PUBLIC - VARIABLES

        /// <summary>
        /// Current instance of the NeuroGuideRecorderSystem instantiated during Create()
        /// </summary>
        public static NeuroGuideRecorderSystem system;

        #endregion

        #region PRIVATE - VARIABLES

        #endregion

        #region PUBLIC - ON DESTROY

        /// <summary>
        /// WHen this object is destroyed, clean up any threads, cororoutines, and listeners
        /// </summary>
        //--------------------------------//
        protected override void OnDestroy()
        //--------------------------------//
        {

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
            NotInitialized,
            Initialized,
            Recording,
            Playing,
            Paused
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
            /// The current state of the NeuroGuide hardware
            /// </summary>
            public State state = State.NotInitialized;

            /// <summary>
            /// Unity action to call when the hardware state has changed
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

            //If the user didn't pass in any options, use the defaults
            if(options == null)
                options = new Options();

            //Generate a NeuroGuideSystem object
            system = new NeuroGuideRecorderSystem();
            system.options = options;

            //Mark the system as initialized, so Update methods know we're ready
            system.state = State.Initialized;
            SendStateUpdatedMessage();

            //If we were unable to make a connection to the NeuroGuide hardware, we cannot continue
            if(system.state == State.NotInitialized)
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

        #region PRIVATE - SEND UDP DATA

        /// <summary>
        /// Sends a byte via our UDP Sender client, to simulate the hardware sending out a byte of data
        /// This will get picked up by our UDP listener client
        /// </summary>
        /// <param name="data"></param>
        //----------------------------------------------//
        private void SendUDPData( byte data )
        //----------------------------------------------//
        {
            NeuroGuideManager.SendUDPData( data );

        } //END SendUDPData Method

        #endregion

        #region PRIVATE - SEND STATE CHANGED MESSAGE

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

            //Debug.Log( "NeuroGuideRecorderManager.cs SendStateUpdatedMessage() state = " + system.state.ToString() );
            system.OnStateUpdate?.Invoke( system.state );

        } //END SendStateUpdatedMessage Method

        #endregion


    } //END NeuroGuideRecorderManager Class

} //END gambit.neuroguide.recorder Namespace