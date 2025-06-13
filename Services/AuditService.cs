using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Mathematics;
using VampireCommandFramework;

namespace KindredCommands.Services;

class AuditMiddleware : CommandMiddleware
{
	public override void BeforeExecute(ICommandContext ctx, CommandAttribute attribute, MethodInfo method)
	{
		var chatCommandContext = (ChatCommandContext)ctx;

		var commandName = method.DeclaringType.Assembly.GetName().Name;

		if (method.DeclaringType.IsDefined(typeof(CommandGroupAttribute)))
		{
			var groupAttribute = (CommandGroupAttribute)Attribute.GetCustomAttribute(method.DeclaringType, typeof(CommandGroupAttribute), false);
			commandName += "." + groupAttribute.Name;
		}

		commandName += "." + attribute.Name;

		Core.AuditService.LogCommandUsage(chatCommandContext.Event.User, commandName);
	}
}

internal class AuditService : IDisposable
{
	static readonly string CONFIG_PATH = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
	static readonly string AUDIT_PATH = Path.Combine(CONFIG_PATH, $"audit-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.csv");

	// Define the datetime format with milliseconds once for consistency
	private const string DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";

	private readonly ConcurrentQueue<string> _auditQueue = new();
	private readonly AutoResetEvent _signal = new(false);
	private readonly Task _processingTask;
	private bool _isDisposed = false;
	private readonly CancellationTokenSource _cancellationTokenSource = new();

	public AuditService()
	{
		var canCommandExecuteMethod = AccessTools.Method(typeof(CommandRegistry), "CanCommandExecute");
		var postfix = new HarmonyMethod(typeof(AuditService), nameof(CanCommandExecutePostfix));
		Plugin.Harmony.Patch(canCommandExecuteMethod, postfix: postfix);

		HookUpForSeeingIfCheckingPermission();

		var auditMiddleware = new AuditMiddleware();
		CommandRegistry.Middlewares.Add(auditMiddleware);

		// Make sure directory exists
		Directory.CreateDirectory(CONFIG_PATH);

		// Start the background processing task
		_processingTask = Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);
	}


	private async Task ProcessQueueAsync()
	{
		while (!_cancellationTokenSource.Token.IsCancellationRequested)
		{
			// Wait for signal or timeout (checking periodically helps ensure we eventually exit)
			await Task.Run(() => _signal.WaitOne(TimeSpan.FromSeconds(1)), _cancellationTokenSource.Token);

			// Process all current items in the queue
			while (_auditQueue.TryDequeue(out var message) && !_cancellationTokenSource.Token.IsCancellationRequested)
			{
				try
				{
					// Write to file
					File.AppendAllText(AUDIT_PATH, message);
				}
				catch (Exception ex)
				{
					// Log error but continue processing
					Console.WriteLine($"Error writing to audit log: {ex.Message}");
				}
			}
		}
	}

	static void CanCommandExecutePostfix(ICommandContext ctx, Object command, ref bool __result)
	{
		// Only log rejected commands when they come from ExecuteCommandWithArgs but not help commands
		if (__result) return;
		if (!InExecute) return;

		var chatCommandContext = (ChatCommandContext)ctx;
		string commandName;

		// Use reflection to access the properties of the command object regardless of its type
		try
		{
			// Get the Method property
			PropertyInfo methodProp = command.GetType().GetProperty("Method");
			if (methodProp == null) return;

			MethodInfo methodInfo = methodProp.GetValue(command) as MethodInfo;
			if (methodInfo == null) return;

			// Start building the command name
			commandName = methodInfo.DeclaringType.Assembly.GetName().Name;

			// Get the GroupAttribute property
			PropertyInfo groupAttributeProp = command.GetType().GetProperty("GroupAttribute");
			if (groupAttributeProp != null)
			{
				var groupAttribute = groupAttributeProp.GetValue(command) as CommandGroupAttribute;
				if (groupAttribute != null)
				{
					commandName += "." + groupAttribute.Name;
				}
			}

			// Get the Attribute property
			PropertyInfo attributeProp = command.GetType().GetProperty("Attribute");
			if (attributeProp == null) return;

			var attribute = attributeProp.GetValue(command) as CommandAttribute;
			if (attribute == null) return;

			commandName += "." + attribute.Name;

			Core.AuditService.LogRejectCommand(chatCommandContext.User, commandName);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in CanCommandExecutePostfix: {ex.Message}");
		}
	}


	static string CsvEscape(string field)
	{
		if (field == null) return "";
		bool mustQuote = field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
		if (!mustQuote) return field;
		return "\"" + field.Replace("\"", "\"\"") + "\"";
	}

	public void LogChatMessage(User fromUser, User toUser, ChatMessageType type, string message)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("chat");
		sb.Append(",");
		sb.Append(fromUser.PlatformId);
		sb.Append(",");
		sb.Append(fromUser.CharacterName);
		sb.Append(",");
		sb.Append(type);
		sb.Append(",");
		sb.Append(toUser.PlatformId);
		sb.Append(",");
		sb.Append(toUser.CharacterName);
		sb.Append(",");
		sb.Append(CsvEscape(message));
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogCommandUsage(User user, string command)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("command");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(command);
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogRejectCommand(User user, string commandName)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("rejectCommand");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(commandName);
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogMapTeleport(User user, PlayerTeleportDebugEvent.TeleportTarget teleportTarget, float3 destination)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("teleportMap");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(teleportTarget);
		sb.Append(",");
		sb.Append(destination);
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogTeleport(User user, TeleportDebugEvent.TeleportTarget teleportTarget, float3 mousePosition,
		TeleportDebugEvent.TeleportLocation location, float3 locationPosition)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("teleport");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(teleportTarget);
		sb.Append(",");
		sb.Append(mousePosition);
		sb.Append(",");
		sb.Append(location);
		sb.Append(",");
		sb.Append(locationPosition);
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogDestroy(User user, string what, DestroyDebugEvent.DestroyWhere where, PrefabGUID prefabGuid, float3 position, int amount)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("destroy");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(what);
		sb.Append(",");
		sb.Append(where);
		sb.Append(",");
		sb.Append(prefabGuid.LookupName());
		sb.Append(",");
		sb.Append(position);
		sb.Append(",");
		sb.Append(amount);
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogGive(User user, PrefabGUID prefabGuid, int amount)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("give");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(prefabGuid.LookupName());
		sb.Append(",");
		sb.Append(amount);
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogBecomeObserver(User user, int mode)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("becomeObserver");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(mode);
		sb.Append("\n");
		AddAuditString(sb);
	}

	public void LogCastleHeartAdmin(User user, CastleHeartInteractEventType eventType, NetworkId castleHeart, int userIndex)
	{
		var sb = new StringBuilder();
		sb.Append(DateTime.Now.ToString(DATE_TIME_FORMAT));
		sb.Append(",");
		sb.Append("castleHeartAdmin");
		sb.Append(",");
		sb.Append(user.PlatformId);
		sb.Append(",");
		sb.Append(user.CharacterName);
		sb.Append(",");
		sb.Append(eventType);
		sb.Append(",");
		sb.Append(castleHeart);
		sb.Append(",");
		sb.Append(userIndex);
		sb.Append("\n");
		AddAuditString(sb);
	}

	void AddAuditString(StringBuilder sb)
	{
		if (_isDisposed) return;

		// Add the message to the queue
		_auditQueue.Enqueue(sb.ToString());

		// Signal that there's work to do
		_signal.Set();
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		_isDisposed = true;

		// Cancel the processing task
		_cancellationTokenSource.Cancel();

		// Wait for processing to complete
		try
		{
			_processingTask.Wait(TimeSpan.FromSeconds(5));
		}
		catch (TaskCanceledException)
		{
			// This is expected
		}

		// Dispose managed resources
		_cancellationTokenSource.Dispose();
		_signal.Dispose();
	}

	static bool inExecuteCommandWithArgs = false;
	static bool inHelpCmd = false;
	static bool InExecute => inExecuteCommandWithArgs && !inHelpCmd;

	static void EnterExecuteCommandWithArgs()
	{
		inExecuteCommandWithArgs = true;
	}

	static void ExitExecuteCommandWithArgs()
	{
		inExecuteCommandWithArgs = false;
	}

	static void EnterHelpCommand()
	{
		inHelpCmd = true;
	}

	static void ExitHelpCommand()
	{
		inHelpCmd = false;
	}

	static void HookUpForSeeingIfCheckingPermission()
	{
		var executeCommandWithArgsMethod = AccessTools.Method(typeof(CommandRegistry), "ExecuteCommandWithArgs");
		if (executeCommandWithArgsMethod == null)
		{
			executeCommandWithArgsMethod = AccessTools.Method(typeof(CommandRegistry), "Handle");
		}

		var prefixExecute = new HarmonyMethod(typeof(AuditService), nameof(EnterExecuteCommandWithArgs));
		var postfixExecute = new HarmonyMethod(typeof(AuditService), nameof(ExitExecuteCommandWithArgs));
		Plugin.Harmony.Patch(executeCommandWithArgsMethod, prefix: prefixExecute, postfix: postfixExecute);

		var prefixHelp = new HarmonyMethod(typeof(AuditService), nameof(EnterHelpCommand));
		var postfixHelp = new HarmonyMethod(typeof(AuditService), nameof(ExitHelpCommand));

		// Use reflection to get the internal static property AssemblyCommandMap
		var assemblyCommandMapProp = typeof(CommandRegistry).GetProperty(
			"AssemblyCommandMap",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

		if (assemblyCommandMapProp == null)
			return;

		var assemblyCommandMap = assemblyCommandMapProp.GetValue(null) as System.Collections.IDictionary;
		if (assemblyCommandMap == null)
			return;

		foreach (System.Collections.DictionaryEntry asmEntry in assemblyCommandMap)
		{
			if (asmEntry.Key is Assembly asm && asm.GetName().Name == "VampireCommandFramework")
			{
				var commandDict = asmEntry.Value as System.Collections.IDictionary;
				if (commandDict == null)
					continue;

				foreach (System.Collections.DictionaryEntry cmdEntry in commandDict)
				{
					var metadata = cmdEntry.Key;
					var commandList = cmdEntry.Value as System.Collections.IEnumerable;
					if (commandList == null)
						continue;

					// Check if any command string is ".help" or ".help-all"
					bool isHelp = false;
					foreach (var cmd in commandList)
					{
						if (cmd is string s && (s == ".help" || s == ".help-all"))
						{
							isHelp = true;
							break;
						}
					}
					if (!isHelp)
						continue;

					// Get the MethodInfo from the metadata (reflection, since it's an internal record)
					var methodProp = metadata.GetType().GetProperty("Method");
					if (methodProp == null)
						continue;

					var methodInfo = methodProp.GetValue(metadata) as MethodInfo;
					if (methodInfo == null)
						continue;

					// Patch the help command method
					Plugin.Harmony.Patch(methodInfo, prefix: prefixHelp, postfix: postfixHelp);
				}
			}
		}
	}
}
