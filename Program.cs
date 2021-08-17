using Sandbox.ModAPI.Ingame;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region mdk preserve

        #region mdk macros
        // Last Build: $MDK_DATETIME$
        #endregion

        #endregion

        /* -----------------------------------------------------------
         * -- User Editable Variables --
         * ----------------------------------------------------------- */

        /// <summary>
        /// Default Retraction Speed
        /// Speed (in m/s) when not otherwise defined
        /// </summary>
        public const float defaultRetractSpeed = 0.5f;

        /// <summary>
        /// Default Extention Speed
        /// Speed (in m/s) when not otherwise defined
        /// </summary>
        public const float defaultExtendSpeed = 0.5f;

        /// <summary>
        /// Default - Auto Retract
        /// Set if not otherwise defined
        /// </summary>
        public const bool defaultAutoRetract = false;

        /// <summary>
        /// Default - Auto Extend
        /// Set if not otherwise defined
        /// </summary>
        public const bool defaultAutoExtend = false;

        /* -----------------------------------------------------------
         * -- Do Not Edit Below This Line! --
         * ----------------------------------------------------------- */

        private static readonly MyIni _iniSystem = new MyIni();
        private static readonly string _sectionKey = "Piston Settings";

        /// <summary>
        /// List of commmands our program will respond too via the arguments box
        /// </summary>
        private Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This is the programs output to be printed on the programmable block pane
        /// </summary>
        private StringBuilder _outputText;

        /// <summary>
        /// Container for storing various speeds in addition to the piston
        /// </summary>
        struct PistonData
        {
            public bool AutoRetract;
            public bool AutoExtend;
            public float RetractSpeed;
            public float ExtendSpeed;
            public IMyPistonBase Piston;
        };

        /// <summary>
        /// Linked list storing our affected pistons, as well as the various speeds
        /// </summary>
        List<PistonData> _pistonDataList;

        /// <summary>
        /// Our program constructor
        /// This is run on compilation (world load, script import, etc)
        /// </summary>
        public Program()
        {
            // Command list
            _commands["reset"] = Init;
            _commands["clear"] = ClearMessage;

            // property inializers
            _pistonDataList = new List<PistonData>();
            _outputText = new StringBuilder();
            _lastMessageText = new StringBuilder();
            _activityIndicator = 0;

            // Run frequency
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            // Initialization - Populate the Lists
            Init();
        }

        /// <summary>
        /// The program entrypoint.
        /// </summary>
        /// <param name="arguments">The string supplied by the user</param>
        /// <param name="updateSource">The source of this update,  whether by the user, a timer, or continous running</param>
        public void Main(string arguments, UpdateType updateSource)
        {
            _outputText.Clear();
            _outputText.AppendLine("-- Configurable Pistons --");
            _outputText.AppendLine();
            _outputText.AppendFormat("  Pistons Monitored: {0:d}", _pistonDataList.Count());
            _outputText.AppendLine();
            _outputText.AppendLine();
            _outputText.AppendLine("  - Stats -");
            _outputText.AppendFormat("  Runtime {0:F3} ms every {1:F0} ms", Runtime.LastRunTimeMs, Runtime.TimeSinceLastRun.TotalMilliseconds);
            _outputText.AppendLine();

            MyCommandLine _commandLine = new MyCommandLine();

            // Handing user input
            if (updateSource == UpdateType.Terminal) {
                if (_commandLine.TryParse(arguments)) {
                    Action commandAction;
                    string command = _commandLine.Argument(0);

                    if (_commands.TryGetValue(command, out commandAction)) {
                        SetMessage("Executing: {0:s}", command);
                        commandAction();
                    }
                    else {
                        StringBuilder msg = new StringBuilder();

                        msg.AppendFormat("  Unknown Command: {0:s}\n", command);
                        msg.AppendLine();
                        msg.AppendLine("  Available Commands:");
                        msg.AppendLine("    reset     Reload settings");
                        msg.AppendLine("    clear     Clears the \"Last Message\"");
                        SetMessage(msg.ToString());

                        msg.Clear();
                    }
                }
            }
            // Handle self-updates (Once, Update1, 10, 100, etc)
            else if ((updateSource & (UpdateType.Once | UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0) {
                foreach (PistonData data in _pistonDataList) {

                    // skip if the block no longer exists in the world
                    // TODO: remove the piston from the list
                    if (data.Piston.Closed) {
                        if (_pistonDataList.Remove(data)) {
                            SetMessage("A Piston was removed from grid\nDeleted Reference.");
                        }

                        break;
                    }

                    // Skip if powered off or in a non-functional state
                    if (!data.Piston.IsWorking) {
                        continue;
                    }

                    switch (data.Piston.Status) {
                        case PistonStatus.Extended:
                            data.Piston.Velocity = ((data.AutoRetract) ? -data.RetractSpeed : -data.RetractSpeed);
                            break;

                        case PistonStatus.Extending:
                            data.Piston.Velocity = data.ExtendSpeed;
                            break;

                        case PistonStatus.Retracted:
                            data.Piston.Velocity = ((data.AutoExtend) ? data.ExtendSpeed : -data.ExtendSpeed);
                            break;

                        case PistonStatus.Retracting:
                            data.Piston.Velocity = -data.RetractSpeed;
                            break;
                    }
                }
            }

            _outputText.AppendStringBuilder(_lastMessageText);
            _outputText.AppendLine();
            _outputText.Append(GetActivityIndicator());

            Print(_outputText.ToString());
        }

        /// <summary>
        /// Setup our internal list of pistons and associated data
        /// </summary>
        public void Init()
        {
            _pistonDataList.Clear();

            List<IMyPistonBase> _pistonList = new List<IMyPistonBase>();
            PistonData data;

            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(_pistonList, Piston => MyIni.HasSection(Piston.CustomData, _sectionKey));

            foreach (IMyPistonBase Piston in _pistonList) {
                if (_iniSystem.TryParse(Piston.CustomData, _sectionKey)) {
                    data = new PistonData();

                    data.Piston = Piston;
                    data.RetractSpeed = _iniSystem.Get(_sectionKey, "RetractSpeed").ToSingle(defaultRetractSpeed);
                    data.ExtendSpeed = _iniSystem.Get(_sectionKey, "ExtendSpeed").ToSingle(defaultExtendSpeed);
                    data.AutoRetract = _iniSystem.Get(_sectionKey, "AutoRetract").ToBoolean();
                    data.AutoExtend = _iniSystem.Get(_sectionKey, "AutoExtend").ToBoolean();
                    _pistonDataList.Add(data);
                }
            }

            _pistonList.Clear();

            SetMessage("Piston cache updated...");
        }

        public enum ErrorLevel : int
        {
            L_NONE,
            L_INFO,
            L_WARNING,
            L_ERROR,
            L_ALL
        }

        private const ErrorLevel OutputLevel = ErrorLevel.L_ALL;

        /// <summary>
        /// Print a message to the message pane
        /// </summary>
        /// <param name="Message">The string to print</param>
        /// <param name="level">The notification level to use (for restricting output)</param>
        public void Print(string Message, ErrorLevel level = ErrorLevel.L_INFO)
        {
            if (level > OutputLevel) {
                return;
            }

            Echo(Message);
        }

        /// <summary>
        /// The last message to be displayed, this allows persistance along with an activity indicator
        /// </summary>
        private StringBuilder _lastMessageText;

        public void SetMessage(string Message)
        {
            _lastMessageText.Clear();
            _lastMessageText.AppendLine();
            _lastMessageText.AppendLine("  - Message -");
            _lastMessageText.AppendLine(Message);
            _lastMessageText.AppendLine();
        }

        public void SetMessage(string format, params object[] args)
        {
            _lastMessageText.AppendFormat(format, args);
        }

        public void ClearMessage()
        {
            _lastMessageText.Clear();
        }

        /// <summary>
        /// This is an internal counter for the little . .. ... .... indicator
        /// </summary>
        private int _activityIndicator;

        /// <summary>
        /// Returns an ever changing string to let the user know the script is "working"
        /// </summary>
        /// <returns>An ever changing string</returns>
        private string GetActivityIndicator()
        {
            string[] _indicators = {
                "    ",
                ".   ",
                " .  ",
                "  . ",
                "   .",
            };

            if (_activityIndicator >= _indicators.Length) {
                _activityIndicator = 0;
            }

            return _indicators[_activityIndicator++];
        }
    }
}