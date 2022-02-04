using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
	// ReSharper disable once ClassNeverInstantiated.Global
	public partial class Program : MyGridProgram
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
		private readonly float defaultRetractSpeed = 0.5f;

		/// <summary>
		/// Default Extension Speed
		/// Speed (in m/s) when not otherwise defined
		/// </summary>
		private readonly float defaultExtendSpeed = 0.5f;

		/// <summary>
		/// Default - Auto Retract
		/// Set if not otherwise defined
		/// </summary>
		private readonly bool defaultAutoRetract = false;

		/// <summary>
		/// Default - Auto Extend
		/// Set if not otherwise defined
		/// </summary>
		private readonly bool defaultAutoExtend = false;

		/* -----------------------------------------------------------
		 * -- Do Not Edit Below This Line! --
		 * ----------------------------------------------------------- */

		/// <summary>
		/// Instance to the INI system
		/// </summary>
		// ReSharper disable once InconsistentNaming
		private static readonly MyIni ini = new MyIni();

		/// <summary>
		/// The [section] to be read from in Custom Data
		/// </summary>
		private static readonly string SectionKey = "Piston Settings";

		/// <summary>
		/// List of commands our program will respond too via the arguments box
		/// </summary>
		private readonly Dictionary<string, Action> commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// This is the programs output to be printed on the programmable block pane
		/// </summary>
		private readonly StringBuilder outputText;

		/// <summary>
		/// Container for storing various speeds in addition to the piston
		/// </summary>
		private struct PistonData
		{
			public bool AutoRetract;
			public bool AutoExtend;
			public float RetractSpeed;
			public float ExtendSpeed;
			public IMyPistonBase Piston;
		}

		/// <summary>
		/// Linked list storing our affected pistons, as well as the various speeds
		/// </summary>
		private readonly List<PistonData> pistonDataList;

		/// <summary>
		/// The last message to be displayed, this allows persistence along with an activity indicator
		/// </summary>
		private readonly StringBuilder lastMessageText;

		/// <summary>
		/// Our program constructor
		/// This is run on compilation (world load, script import, etc)
		/// </summary>
		public Program()
		{
			// Command list
			commands["reset"] = Init;

			// property initializers
			pistonDataList = new List<PistonData>();
			outputText = new StringBuilder();
			lastMessageText = new StringBuilder();
			__activityIndicator = 0;

			// Run frequency
			Runtime.UpdateFrequency = UpdateFrequency.Once;
		}

		/// <summary>
		/// The program entry-point.
		/// </summary>
		/// <param name="arguments">The string supplied by the user</param>
		/// <param name="updateSource">The source of this update,  whether by the user, a timer, or continous running</param>
		public void Main(string arguments, UpdateType updateSource)
		{
			outputText.Clear();
			outputText.AppendFormat(
				"-- Configurable Pistons --" +
				"\n" +
				"  Pistons Monitored: {0:d}" +
				"\n" +
				"\n" +
				"  - Stats -\n" +
				"  Runtime {0:F3} ms every {1:F0} ms\n" +
				"\n",
				pistonDataList.Count(),
				Runtime.LastRunTimeMs,
				Runtime.TimeSinceLastRun.TotalMilliseconds
			);

			// Handing user input
			if (updateSource == UpdateType.Terminal) {
				MyCommandLine commandLine = new MyCommandLine();

				if (commandLine.TryParse(arguments)) {
					string command = commandLine.Argument(0);
					Action commandAction;

					if (commands.TryGetValue(command, out commandAction)) {
						SetMessage("Executing: {0:s}", command);
						commandAction();
					}
					else {
						StringBuilder msg = new StringBuilder();

						msg.AppendFormat(
							"  Unknown Command: {0:s}\n" +
							"\n" +
							"  Available Commands:\n" +
							"    reset     Reload settings\n",
							command
						);

						SetMessage(msg.ToString());

						msg.Clear();
					}
				}
			}
			else if ((updateSource & UpdateType.Update10) != 0) {
				foreach (PistonData data in pistonDataList) {
					// skip if the block no longer exists in the world
					if (data.Piston.Closed) {
						if (pistonDataList.Remove(data)) {
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
							data.Piston.Velocity = data.AutoRetract ? -data.RetractSpeed : data.ExtendSpeed;
							break;

						case PistonStatus.Extending:
							data.Piston.Velocity = data.ExtendSpeed;
							break;

						case PistonStatus.Retracted:
							data.Piston.Velocity = data.AutoExtend ? data.ExtendSpeed : -data.RetractSpeed;
							break;

						case PistonStatus.Retracting:
							data.Piston.Velocity = -data.RetractSpeed;
							break;
					}
				}

				// no pistons controlled,  stop continuously running
				if (pistonDataList.Count == 0) {
					Runtime.UpdateFrequency = UpdateFrequency.None;
				}
			}
			else if ((updateSource & UpdateType.Once) != 0) {
				// Initialization - Populate the Lists
				Init();

				if (pistonDataList.Count > 0) {
					Runtime.UpdateFrequency = UpdateFrequency.Update10;
				}
			}

			outputText.AppendStringBuilder(lastMessageText);
			outputText.AppendLine();
			outputText.Append(GetActivityIndicator());

			Print(outputText.ToString());
		}

		/// <summary>
		/// Setup our internal list of pistons and associated data
		/// </summary>
		private void Init()
		{
			pistonDataList.Clear();

			// temporary list
			List<IMyPistonBase> pistonList = new List<IMyPistonBase>();

			GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistonList, piston => MyIni.HasSection(piston.CustomData, SectionKey));

			foreach (IMyPistonBase piston in pistonList) {
				if (!ini.TryParse(piston.CustomData, SectionKey)) {
					continue;
				}

				PistonData data = new PistonData {
					Piston = piston,
					RetractSpeed = ini.Get(SectionKey, "RetractSpeed").ToSingle(defaultRetractSpeed),
					ExtendSpeed = ini.Get(SectionKey, "ExtendSpeed").ToSingle(defaultExtendSpeed),
					AutoRetract = ini.Get(SectionKey, "AutoRetract").ToBoolean(defaultAutoRetract),
					AutoExtend = ini.Get(SectionKey, "AutoExtend").ToBoolean(defaultAutoExtend)
				};

				pistonDataList.Add(data);
			}

			pistonList.Clear();

			SetMessage("Piston cache updated...");
		}

		private void SetMessage(string message)
		{
			lastMessageText.Clear();
			lastMessageText.AppendFormat("\n  - Message -\n{0}\n\n", message);
		}

		private void SetMessage(string format, params object[] args)
		{
			lastMessageText.Clear();
			lastMessageText.AppendFormat(format, args);
		}

		// ReSharper disable once UnusedMember.Local
		private void ClearMessage()
		{
			lastMessageText.Clear();
		}

		/// <summary>
		/// This is an internal counter for the little . .. ... .... indicator
		/// </summary>
		// ReSharper disable once InconsistentNaming
		private int __activityIndicator;

		/// <summary>
		/// This is the internal string array for the indicator
		/// </summary>
		// ReSharper disable once InconsistentNaming
		private readonly string[] __indicators = { "    ", ".   ", " .  ", "  . ", "   .", };

		/// <summary>
		/// Returns an ever changing string to let the user know the script is "working"
		/// </summary>
		/// <returns>An ever changing string</returns>
		private string GetActivityIndicator()
		{
			if (__activityIndicator >= __indicators.Length) {
				__activityIndicator = 0;
			}

			return __indicators[__activityIndicator++];
		}

		/// <summary>
		/// Print a message to the message pane
		/// </summary>
		/// <param name="message">The string to print</param>
		private void Print(string message)
		{
			Echo(message);
		}

		private void Print(string message, IMyTextSurface surface)
		{
			if (surface != null) {
				surface.WriteText(message);
			}

			Echo(message);
		}
	}
}
