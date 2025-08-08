#region IMPORTS

#if EXT_DOTWEEN
using DG.Tweening;
#endif

#if GAMBIT_NEUROGUIDE
using gambit.neuroguide;
#endif

using gambit.neuroguide;
using UnityEngine;

#endregion

/// <summary>
/// Simple test to show how to respond to NeuroGuide experience value changes
/// </summary>
public class NeuroGuideInteractableDemo: MonoBehaviour, INeuroGuideAnimationExperienceInteractable
{

    #region PUBLIC - VARIABLES

    /// <summary>
    /// Cube used to demo the NeuroGuide Interactable interface functionality
    /// </summary>
    public GameObject cube;

    #endregion

    #region PUBLIC - ON ABOVE THRESHOLD

    /// <summary>
    /// Called when the score goes above the threshold.
    /// Once the score falls below the threshold, 
    /// we wait for a timer to complete before we can call this again when we go above the threshold
    /// </summary>
    //----------------------------------//
    public void OnAboveThreshold()
    //----------------------------------//
    {
        //Debug.Log( "OnAboveThreshold" );

    } //END OnAboveThreshold

    #endregion

    #region PUBLIC - ON BELOW THRESHOLD

    /// <summary>
    /// Called when the score goes above the threshold, then falls back below it
    /// </summary>
    //--------------------------------//
    public void OnBelowThreshold()
    //--------------------------------//
    {
        //Debug.Log( "OnBelowThreshold" );

    } //END OnBelowThreshold Method

    #endregion

    #region PUBLIC - ON RECIEVING REWARD UPDATE

    /// <summary>
    /// Called when the user start or stops recieving a reward
    /// </summary>
    /// <param name="isRecievingReward">Is the user recieving a reward?</param>
    //------------------------------------------------------------------------------------------//
    public void OnRecievingRewardChanged( bool isRecievingReward )
    //------------------------------------------------------------------------------------------//
    {
        //Debug.Log( "NeuroGuideInteractableDemo.cs OnRecievingRewardChanged() state = " + isRecievingReward.ToString() );

    } //END OnRecievingRewardChanged Method

    #endregion

    #region PUBLIC - ON DATA UPDATE

    /// <summary>
    /// Called when the NeuroGuideExperience updates the users progress in the experience
    /// </summary>
    /// <param name="normalizedValue">Progress, normalized 0-1 value</param>
    //------------------------------------------------------------------------------------------//
    public void OnDataUpdate( float normalizedValue )
    //------------------------------------------------------------------------------------------//
    {
        if(cube == null)
            return;

        //Debug.Log( normalizedValue );

#if EXT_DOTWEEN
        Vector3 scale = new Vector3( normalizedValue, normalizedValue, normalizedValue );
        cube.transform.DOScale( scale, .25f );
#endif

    } //END OnDataUpdate Method

    #endregion

} //END NeuroGuideInteractableDemo Class