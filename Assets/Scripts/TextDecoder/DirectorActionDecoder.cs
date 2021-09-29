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

    private Dictionary<string, Delegate> _actionMap;

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
    public void Start()
    {
        _actionMap = new Dictionary<string, System.Delegate> {
            {"SHOWACTOR", (Action<bool>) (showActor => {
                if (showActor) { SceneController.ShowActor(); }
                else { SceneController.HideActor(); }
            })},
            {"ACTOR", (Action<string>) (actor => {
                ActorController.SetActiveActor(actor);
            })},
            {"SPEAK", (Action<string>) (actor => {
                SetSpeaker(actor, SpeakingType.Speaking);
            })},
            {"THINK", (Action<string>) (actor => {
                SetSpeaker(actor, SpeakingType.Speaking);
            })},
            {"SET_POSE", (Action<string>) (pose => {
                ActorController.SetPose(pose);
            })},
            {"PLAY_EMOTION", (Action<string>) (animation => {
                ActorController.PlayEmotion(animation);
            })},
            {"FADE_IN", (Action<float>) (seconds => {
                SceneController.FadeIn(seconds);
            })},
            {"FADE_OUT", (Action<float>) (seconds => {
                SceneController.FadeOut(seconds);
            })},
            {"SHAKESCREEN", (Action<float>) (intensity => {
                SceneController.ShakeScreen(intensity);
            })},
            {"SCENE", (Action<string>) (sceneName => {
                SceneController.SetScene(sceneName);
            })},
            {"CAMERA_SET", (Action<int, int>) ((x, y) => {
                SceneController.SetCameraPos(new Vector2Int(x, y));
            })},
            {"CAMERA_PAN", (Action<float, int, int>) ((duration, x, y) => {
                SceneController.PanCamera(duration, new Vector2Int(x, y));
            })},
            {"SHOW_ITEM", (Action<string, itemDisplayPosition>) ((itemName, itemDisplayPosition) => {
                SceneController.ShowItem(itemName, itemDisplayPosition);
            })},
            {"HIDE_ITEM", (Action) (() => {
                SceneController.HideItem();
            })},
            {"WAIT", (Action<float>) (secondsFloat => {
                SceneController.Wait(secondsFloat);
            })},
            {"PLAYSFX", (Action<string>) (sfx => {
                AudioController.PlaySFX(sfx);
            })},
            {"PLAYSONG", (Action<string>) (songName => {
                AudioController.PlaySong(songName);
            })},
            {"STOP_SONG", (Action) (() => {
                AudioController.StopSong();
            })},
            {"ADD_EVIDENCE", (Action<string>) (evidence => {
                EvidenceController.AddEvidence(evidence);
            })},
            {"REMOVE_EVIDENCE", (Action<string>) (evidence => {
                EvidenceController.RemoveEvidence(evidence);
            })},
            {"ADD_RECORD", (Action<string>) (actor => {
                EvidenceController.AddToCourtRecord(actor);
            })},
            {"PRESENT_EVIDENCE", (Action) (() => {
                EvidenceController.OpenEvidenceMenu();
            })},
            {"SUBSTITUTE_EVIDENCE", (Action<string>) (evidence => {
                EvidenceController.SubstituteEvidenceWithAlt(evidence);
            })},
            {"DIALOG_SPEED", (Action<string>) (parameters => {
                AppearingDialogController.SetTimerValue(WaiterType.Dialog, parameters);
            })},
            {"OVERALL_SPEED", (Action<string>) (parameters => {
                AppearingDialogController.SetTimerValue(WaiterType.Overall, parameters);
            })},
            {"PUNCTUATION_SPEED", (Action<string>) (parameters => {
                AppearingDialogController.SetTimerValue(WaiterType.Punctuation, parameters);
            })},
            {"CLEAR_SPEED", (Action) (() => {
                AppearingDialogController.ClearAllWaiters();
            })},
            {"DISABLE_SKIPPING", (Action<bool>) (disabled => {
                AppearingDialogController.ToggleDisableTextSkipping(disabled);
            })},
            {"AUTOSKIP", (Action<bool>) (shouldSkipDialog => {
                AppearingDialogController.AutoSkipDialog(shouldSkipDialog);
            })},
            {"CONTINUE_DIALOG", (Action) (() => {
                AppearingDialogController.ContinueDialog();
            })},
            {"APPEAR_INSTANTLY", (Action) (() => {
                AppearingDialogController.PrintTextInstantly = true;
            })},
            {"HIDE_TEXTBOX", (Action) (() => {
                AppearingDialogController.HideTextbox();
            })},
            {"WAIT_FOR_INPUT", (Action) (() => {
                // void
            })}
        };
    }

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

        // Find method with exact same name inside the action map
        if (!_actionMap.ContainsKey(action))
        {
            throw new Parser.ScriptParsingError($"DirectorActionDecoder contains no method named '{action}'");
        }

        MethodInfo method = _actionMap[action].Method;

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
    
}