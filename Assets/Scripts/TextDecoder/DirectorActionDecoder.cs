using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine.Events;
using UnityEngine;

namespace Parser
{
    public interface IParser
    {
        public bool Applies(Type type);
    }
    public abstract class Parser<T> : IParser
    {
        public abstract string Parse(string input, out T output);

        public bool Applies(Type type)
        {
            return typeof(T) == type;
        }
    }

    public class BoolParser : Parser<bool>
    {
        public override string Parse(string input, out bool output)
        {
            if (!bool.TryParse(input, out output))
            {
                return $"Must be either 'true' or 'false'";
            }

            return null;
        }
    }

    public class IntParser : Parser<int>
    {
        public override string Parse(string input, out int output)
        {
            if (!int.TryParse(input, out output))
            {
                return $"Must be a number with no decimals";
            }

            return null;
        }
    }

    public class FloatParser : Parser<float>
    {
        public override string Parse(string input, out float output)
        {
            if (!float.TryParse(input, out output))
            {
                return $"Must be a number (with decimals delimited with '.' instead of ',')";
            }

            return null;
        }
    }

    public class StringParser : Parser<string>
    {
        public override string Parse(string input, out string output)
        {
            output = input;
            return null;
        }
    }


    public class ItemDisplayPositionParser : Parser<itemDisplayPosition>
    {
        public override string Parse(string input, out itemDisplayPosition output)
        {
            if (!Enum.TryParse(input, out output))
            {
                return $"Cannot convert '{input}' into an {typeof(itemDisplayPosition)}";
            }
            return null;
        }
    }

    public class ScriptParsingError : Exception
    {
        public ScriptParsingError(string message) : base(message)
        {
        }
    }
}

public class DirectorActionDecoder : MonoBehaviour
{
    public IActorController ActorController { private get; set; }
    public ISceneController SceneController { private get; set; }
    public IAudioController AudioController { private get; set; }
    public IEvidenceController EvidenceController { private get; set; }
    public IAppearingDialogueController AppearingDialogController { private get; set; }

    [Header("Events")]
    [Tooltip("Event that gets called when the system is done processing the action")]
    [SerializeField] private UnityEvent _onActionDone;

    /// <summary>
    /// Called whenever a new action is executed (encountered and then forwarded here) in the script
    /// </summary>
    /// <param name="line">The full line in the script containing the action and parameters</param>
    public void OnNewActionLine(string line)
    {
        const char actionSideSeparator = ':';
        const char actionParameterSeparator = ',';

        // Split into action and parameter
        string[] actionAndParam = line.Substring(1, line.Length - 2).Split(actionSideSeparator);

        if (actionAndParam.Length > 2)
        {
            Debug.LogError("Invalid action with line: " + line);
            return;
        }

        string action = actionAndParam[0];
        string[] parameters = (actionAndParam.Length == 2) ? actionAndParam[1].Split(actionParameterSeparator) : new string[0];

        // Find method with exact same name as action inside script
        MethodInfo method = this.GetType().GetMethod(action, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new Parser.ScriptParsingError($"DirectorActionDecoder contains no method named '{action}'");
        }

        // For each parameter of that action...
        ParameterInfo[] methodParameters = method.GetParameters();
        if (methodParameters.Length != parameters.Length)
        {
            throw new Parser.ScriptParsingError($"'{action}' requires exactly {methodParameters.Length} parameters (has {parameters.Length} instead)");
        }


        List<object> parsedMethodParameters = new List<object>();
        for (int index = 0; index < methodParameters.Length; index++)
        {
            // Determine it's type
            ParameterInfo methodParameter = methodParameters[index];

            // Construct a parser for it
            Type parser = GetType().Assembly.GetTypes().First(type => type.BaseType is {IsGenericType: true} && type.BaseType.GenericTypeArguments[0] == methodParameter.ParameterType);
            ConstructorInfo parserConstructor = parser.GetConstructor(Type.EmptyTypes);
            if (parserConstructor == null)
            {
                Debug.LogError($"Parser for type {methodParameter.ParameterType} has no constructor without parameters");
                return;
            }

            // Find the 'Parse' method on that parser
            MethodInfo parseMethod = parser.GetMethod("Parse");
            if (parseMethod == null)
            {
                Debug.LogError($"Parser for type {methodParameter.ParameterType} has no 'Parse' method");
                return;
            }

            // Create a parser and call the 'Parse' method
            object parserInstance = parserConstructor.Invoke(new object[0]);
            object[] parseMethodParameters = {parameters[index], null};

            // If we received an error attempting to parse a parameter to the type, expose it to the user
            var humanReadableParseError = parseMethod.Invoke(parserInstance, parseMethodParameters);
            if (humanReadableParseError != null)
            {
                throw new Parser.ScriptParsingError($"'{parameters[index]}' is incorrect as parameter #{index+1} ({methodParameter.Name}) for action '{action}': {humanReadableParseError}");
            }

            parsedMethodParameters.Add(parseMethodParameters[1]);
        }

        // Call the method
        method.Invoke(this, parsedMethodParameters.ToArray());
        _onActionDone.Invoke();
    }

    #region Available actions

    #region ActorController
    /// <summary>
    /// Sets the shown actor in the scene
    /// </summary>
    /// <param name="actor">Actor to be switched to</param>
    private void ACTOR(string actor)
    {
        ActorController.SetActiveActor(actor);
    }

    /// <summary>
    /// Shows or hides the actor based on the string parameter.
    /// </summary>
    /// <param name="showActor">Should contain true or false based on showing or hiding the actor respectively</param>
    private void SHOWACTOR(bool showActor)
    {
        if (showActor)
        {
            SceneController.ShowActor();
        }
        else
        {
            SceneController.HideActor();
        }
    }

    /// <summary>
    /// Set the speaker for the current and following lines, until a new speaker is set
    /// </summary>
    /// <param name="actor">Actor to make the speaker</param>
    /// <param name="speakingType">Type of speaking to speak the text with</param>
    private void SetSpeaker(string actor, SpeakingType speakingType)
    {
        ActorController.SetActiveSpeaker(actor);
        ActorController.SetSpeakingType(speakingType);

    }

    private void SPEAK(string actor)
    {
        SetSpeaker(actor, SpeakingType.Speaking);
    }
    private void THINK(string actor)
    {
        SetSpeaker(actor, SpeakingType.Speaking);
    }

    /// <summary>
    /// Set the pose of the current actor
    /// </summary>
    /// <param name="pose">Pose to display for the current actor</param>
    private void SET_POSE(string pose)
    {
        ActorController.SetPose(pose);
    }

    /// <summary>
    /// Plays an emotion for the current actor. Emotion is a fancy term for animation on an actor.
    /// </summary>
    /// <param name="animation">Animation to play</param>
    private void PLAY_EMOTION(string animation)
    {
        ActorController.PlayEmotion(animation);
    }
    #endregion

    #region SceneController
    /// <summary>
    /// Fades the scene in from black
    /// </summary>
    /// <param name="seconds">Amount of seconds the fade-in should take as a float</param>
    void FADE_IN(float seconds)
    {
        SceneController.FadeIn(seconds);
    }

    /// <summary>
    /// Fades the scene to black
    /// </summary>
    /// <param name="seconds">Amount of seconds the fade-out should take as a float</param>
    void FADE_OUT(float seconds)
    {
        SceneController.FadeOut(seconds);
    }

    /// <summary>
    /// Shakes the screen
    /// </summary>
    /// <param name="intensity">Max displacement of the screen as a float</param>
    void SHAKESCREEN(float intensity)
    {
        SceneController.ShakeScreen(intensity);
    }

    /// <summary>
    /// Sets the scene (background, character location on screen, any props (probably prefabs))
    /// </summary>
    /// <param name="sceneName">Scene to change to</param>
    void SCENE(string sceneName)
    {
        SceneController.SetScene(sceneName);
    }

    /// <summary>
    /// Sets the camera position
    /// </summary>
    /// <param name="position">New camera coordinates in the "int x,int y" format</param>
    void CAMERA_SET(int x, int y)
    {
        SceneController.SetCameraPos(new Vector2Int(x,y));
    }

    /// <summary>
    /// Pan the camera to a certain x,y position
    /// </summary>
    /// <param name="durationAndPosition">Duration the pan should take and the target coordinates in the "float seconds, int x, int y" format</param>
    void CAMERA_PAN(float duration, int x, int y)
    {
        SceneController.PanCamera(duration, new Vector2Int(x, y));
    }

    /// <summary>
    /// Shows an item on the middle, left, or right side of the screen.
    /// </summary>
    /// <param name="ItemNameAndPosition">Which item to show and where to show it, in the "string item, itemPosition pos" format</param>
    void SHOW_ITEM(string itemName, itemDisplayPosition itemDisplayPosition)
    {
        SceneController.ShowItem(itemName, itemDisplayPosition);
    }

    /// <summary>
    /// Hides the item displayed on the screen by ShowItem method.
    /// </summary>
    void HIDE_ITEM()
    {
        SceneController.HideItem();
    }

    /// <summary>
    /// Waits seconds before automatically continuing.
    /// </summary>
    /// <param name="seconds">Amount of seconds to wait</param>
    void WAIT(float secondsFloat)
    {
        SceneController.Wait(secondsFloat);
    }

    #endregion
    
    #region AudioController
    /// <summary>
    /// Plays a sound effect
    /// </summary>
    /// <param name="sfx">Name of the sound effect</param>
    void PLAYSFX(string sfx)
    {
        AudioController.PlaySFX(sfx);
    }

    /// <summary>
    /// Sets the background music
    /// </summary>
    /// <param name="songName">Name of the new song</param>
    void PLAYSONG(string songName)
    {
        AudioController.PlaySong(songName);
    }

    /// <summary>
    /// If music is currently playing, stop it!
    /// </summary>
    void STOP_SONG()
    {
        AudioController.StopSong();
    }
    #endregion

    #region EvidenceController
    void ADD_EVIDENCE(string evidence)
    {
        EvidenceController.AddEvidence(evidence);
    }

    void REMOVE_EVIDENCE(string evidence)
    {
        EvidenceController.RemoveEvidence(evidence);
    }

    void ADD_RECORD(string actor)
    {
        EvidenceController.AddToCourtRecord(actor);
    }

    /// <summary>
    /// Calls the onPresentEvidence event on evidence controller which
    /// opens the evidence menu so evidence can be presented.
    /// </summary>
    void PRESENT_EVIDENCE()
    {
        EvidenceController.OpenEvidenceMenu();
    }

    /// <summary>
    /// Used to substitute a specified Evidence object with its assigned alternate Evidence object.
    /// </summary>
    /// <param name="evidence">The name of the evidence to substitute.</param>
    void SUBSTITUTE_EVIDENCE(string evidence)
    {
        EvidenceController.SubstituteEvidenceWithAlt(evidence);
    }

    #endregion

    #region DialogStuff
    ///<summary>
    ///Changes the dialog speed in appearingDialogController if it has beben set.
    ///</summary>
    ///<param name = "currentWaiterType">The current waiters type which appear time should be changed.</param>
    ///<param name = "parameters">Contains all the parameters needed to change the appearing time.</param>
    private void ChangeDialogSpeed(WaiterType currentWaiterType, string parameters)
    {
        AppearingDialogController.SetTimerValue(currentWaiterType, parameters);
    }

    private void DIALOG_SPEED(string parameters)
    {
        AppearingDialogController.SetTimerValue(WaiterType.Dialog, parameters);
    }

    private void OVERALL_SPEED(string parameters)
    {
        AppearingDialogController.SetTimerValue(WaiterType.Overall, parameters);
    }

    private void PUNCTUATION_SPEED(string parameters)
    {
        AppearingDialogController.SetTimerValue(WaiterType.Punctuation, parameters);
    }

    ///<summary>
    ///Clears all custom set dialog speeds
    ///</summary>
    private void CLEAR_SPEED()
    {
        AppearingDialogController.ClearAllWaiters();
    }

    ///<summary>
    ///Toggles skipping on or off
    ///</summary>
    ///<param name = "disable">Should the text skipping be disabled or not</param>
    private void DISABLE_SKIPPING(bool disabled)
    {
        AppearingDialogController.ToggleDisableTextSkipping(disabled);
    }

    ///<summary>
    ///Forces the next line of dialog happen right after current one.
    ///</summary>
    private void AUTOSKIP(bool shouldSkipDialog)
    {
        AppearingDialogController.AutoSkipDialog(shouldSkipDialog);
    }

    ///<summary>
    ///Makes the new dialog appear after current one.
    ///</summary>
    private void CONTINUE_DIALOG()
    {
        AppearingDialogController.ContinueDialog();
    }

    /// <summary>
    /// Makes the next line of dialogue appear instantly instead of one character at a time.
    /// </summary>
    private void APPEAR_INSTANTLY()
    {
        AppearingDialogController.PrintTextInstantly = true;
    }

    /// <summary>
    /// Hides the dialogue textbox.
    /// </summary>
    private void HIDE_TEXTBOX()
    {
        AppearingDialogController.HideTextbox();
    }

    /// <summary>
    /// Swallows the current input asking the user to press the continue button again
    /// </summary>
    private void WAIT_FOR_INPUT()
    {
    }
    #endregion

    #endregion
}