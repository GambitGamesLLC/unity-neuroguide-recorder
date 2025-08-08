
namespace gambit.neuroguide
{

    #region IMPORTS

#if UNITY_INPUT
    using UnityEngine.InputSystem;
#endif

#if GAMBIT_NEUROGUIDE
    using gambit.neuroguide;
#endif


#if EXT_DOTWEEN
    using DG.Tweening;
#endif

    using UnityEngine;

    #endregion

    /// <summary>
    /// Spawns cubes to test the NeuroGuide hardware package. 
    /// Uses the NeuroGuide Animation Experience system.
    /// Use Up and Down keys to test
    /// </summary>
    public class NeuroGuideAnimationExperienceDemo: MonoBehaviour
    {

        #region PUBLIC - VARIABLES

        /// <summary>
        /// Should we enable the NeuroGuideManager debug logs?
        /// </summary>
        public bool logs = true;

        /// <summary>
        /// Should we enable the debug system for the NeuroGear hardware? This will enable keyboard events to control simulated NeuroGear hardware data spawned during the Create() method of NeuroGuideManager.cs
        /// </summary>
        public bool debug = true;

        /// <summary>
        /// How long should this experience last if the user were to be in a reward state (doesn't have to be consecutively), but their score to get towards the goal lowers when they are not in the reward state
        /// </summary>
        public float totalDurationInSeconds = 5;

        /// <summary>
        /// What should the score be before we call our OnAboveThreshold and OnBelowThreshold callbacks?
        /// </summary>
        public float threshold = 0.9f;

        #endregion

        #region PUBLIC - START

        /// <summary>
        /// Unity lifecycle method
        /// </summary>
        //---------------------------------//
        public void Start()
        //---------------------------------//
        {

            CreateNeuroGuideManager();

        } //END Start Method

        #endregion

        #region PRIVATE - CREATE NEUROGUIDE MANAGER

        /// <summary>
        /// Creates the NeuroGuideManager
        /// </summary>
        //---------------------------------------------//
        private void CreateNeuroGuideManager()
        //---------------------------------------------//
        {

            NeuroGuideManager.Create
            (
                //Create and pass in Options object
                new NeuroGuideManager.Options()
                {
                    showDebugLogs = logs,
                    enableDebugData = debug
                },

                //OnSuccess
                ( NeuroGuideManager.NeuroGuideSystem system ) => {
                    //if( logs ) Debug.Log( "NeuroGuideDemo.cs CreateNeuroGuideManager() Successfully created NeuroGuideManager and recieved system object" );

                    CreateNeuroGuideAnimationExperience();
                },

                //OnFailed
                ( string error ) => {
                    if(logs)
                        Debug.LogWarning( error );
                },

                //OnDataUpdate
                ( NeuroGuideData ) =>
                {
                    //if( logs ) Debug.Log( "NeuroGuideDemo CreateNeuroGuideManager() Hardware Data updated ... data.isRecievingReward = " + data.isRecievingReward );
                },

                //OnStateUpdate
                ( NeuroGuideManager.State state ) =>
                {
                    //if( logs ) Debug.Log( "NeuroGuideDemo.cs CreateNeuroGuideManager() State changed to " + state.ToString() );
                } );

        } //END CreateNeuroGuideManager Method

        //----------------------------------------------//
        private void CreateNeuroGuideAnimationExperience()
        //----------------------------------------------//
        {
            NeuroGuideAnimationExperience.Create
            (
                //Create and Pass in Options object
                new NeuroGuideAnimationExperience.Options()
                {
                    showDebugLogs = logs,
                    totalDurationInSeconds = totalDurationInSeconds,
                    threshold = threshold,
                    OnAboveThreshold = () =>
                    {
                        //Debug.Log( "Above Threshold" );
                    },
                    OnBelowThreshold = () =>
                    {
                        //Debug.Log( "Below Threshold" );
                    },
                    OnDataUpdate = ( float score ) =>
                    {
                        //Debug.Log( score );
                    },
                    OnRecievingRewardChanged = ( bool reward ) =>
                    {
                        //Debug.Log( reward );
                    }
                },

                //OnSuccess
                ( NeuroGuideAnimationExperience.NeuroGuideAnimationExperienceSystem system ) =>
                {
                    //if( logs ) Debug.Log( "CreateNeuroGuideAnimationExperience() OnSuccess" );
                },

                //OnError
                ( string error ) =>
                {
                    if(logs)
                        Debug.Log( error );
                }
            );

        } //END CreateNeuroGuideAnimationExperience Method

        #endregion

    } //END NeuroGuideAnimationExperienceDemo Class

} //END gambit.neuroguide Namespace