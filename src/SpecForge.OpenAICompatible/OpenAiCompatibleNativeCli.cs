using SpecForge.Domain.Application;
using System.Diagnostics;
using System.Text;

namespace SpecForge.OpenAICompatible;

internal static class OpenAiCompatibleNativeCliRunners
{
    public static IEnumerable<INativeCliRunner> Create()
    {
        yield return new SystemCodexCliRunner();
        yield return new SystemClaudeCliRunner();
        yield return new SystemCopilotCliRunner();
    }

    private static string FormatProcessOutputForLog(string? value, bool trimWhitespace = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        var normalized = value.ReplaceLineEndings("\\n");
        if (trimWhitespace)
        {
            normalized = normalized.Trim();
        }

        if (normalized.Length > 320)
        {
            normalized = $"{normalized[..320]}...";
        }

        return $"\"{normalized.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string BuildSanitizedCommandForLog(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? standardInput)
    {
        var sanitizedArguments = new List<string>(arguments.Count + (standardInput is null ? 0 : 1));
        var promptArgumentCollapsed = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!promptArgumentCollapsed && LooksLikeEmbeddedPromptArgument(argument, index, arguments))
            {
                sanitizedArguments.Add($"<prompt:{argument.Length} chars>");
                promptArgumentCollapsed = true;
                continue;
            }

            sanitizedArguments.Add(argument);
        }

        if (standardInput is not null)
        {
            sanitizedArguments.Add($"<stdin:{standardInput.Length} chars>");
        }

        return $"{executablePath} {string.Join(' ', sanitizedArguments)}".TrimEnd();
    }

    private static bool LooksLikeEmbeddedPromptArgument(
        string argument,
        int index,
        IReadOnlyList<string> allArguments)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        if (argument.Contains('\n') || argument.Contains('\r'))
        {
            return true;
        }

        return index == allArguments.Count - 1 && argument.Length > 512;
    }

    internal sealed class SystemCodexCliRunner : NativeCliRunnerBase
    {
        public override string ProviderKind => "codex";

        protected override string ExecutablePathEnvVar => "SPECFORGE_CODEX_CLI_PATH";

        protected override string[] CandidateExecutableNames => ["codex"];

        protected override string? BundledExecutablePath => "/Applications/Codex.app/Contents/Resources/codex";

        protected override IReadOnlyList<string> GetVersionArguments() => ["--version"];

        public override async Task<NativeCliExecutionResult> ExecuteAsync(NativeCliInvocation invocation, CancellationToken cancellationToken)
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"specforge-codex-output-{Guid.NewGuid():N}.txt");

            try
            {
                var arguments = new List<string> { "exec" };

                if (!string.IsNullOrWhiteSpace(invocation.Model))
                {
                    arguments.Add("-m");
                    arguments.Add(invocation.Model);
                }

                if (!string.IsNullOrWhiteSpace(invocation.ReasoningEffort))
                {
                    arguments.Add("-c");
                    arguments.Add($"model_reasoning_effort=\"{invocation.ReasoningEffort}\"");
                }

                arguments.Add("-C");
                arguments.Add(invocation.WorkspaceRoot);
                if (string.Equals(invocation.SandboxMode, "workspace-write", StringComparison.Ordinal))
                {
                    arguments.Add("--full-auto");
                }
                else
                {
                    arguments.Add("--sandbox");
                    arguments.Add(invocation.SandboxMode);
                }

                arguments.Add("--color");
                arguments.Add("never");
                arguments.Add("--json");
                arguments.Add("-o");
                arguments.Add(outputPath);
                arguments.Add("-");

                var result = await RunProcessAsync(arguments, invocation.WorkspaceRoot, cancellationToken, invocation.Prompt);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Codex CLI execution failed with exit code {result.ExitCode}. stderr: {result.StandardError.Trim()} stdout: {result.StandardOutput.Trim()}");
                }

                if (!File.Exists(outputPath))
                {
                    throw new InvalidOperationException("Codex CLI execution completed without writing the expected final response file.");
                }

                var content = await File.ReadAllTextAsync(outputPath, cancellationToken);
                return new NativeCliExecutionResult(content, OpenAiCompatiblePhaseExecutionProvider.TryReadCodexJsonlUsage(result.StandardOutput));
            }
            finally
            {
                TryDelete(outputPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    internal sealed class SystemClaudeCliRunner : NativeCliRunnerBase
    {
        public override string ProviderKind => "claude";

        protected override string ExecutablePathEnvVar => "SPECFORGE_CLAUDE_CLI_PATH";

        protected override string[] CandidateExecutableNames => ["claude"];

        protected override IReadOnlyList<string> GetVersionArguments() => ["--version"];

        public override async Task<NativeCliExecutionResult> ExecuteAsync(NativeCliInvocation invocation, CancellationToken cancellationToken)
        {
            var arguments = new List<string>
            {
                "-p",
                "--output-format",
                "json",
                "--permission-mode",
                "bypassPermissions",
                "--add-dir",
                invocation.WorkspaceRoot
            };

            if (!string.IsNullOrWhiteSpace(invocation.Model))
            {
                arguments.Add("--model");
                arguments.Add(invocation.Model);
            }

            arguments.Add(invocation.Prompt);

            var result = await RunProcessAsync(arguments, invocation.WorkspaceRoot, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Claude CLI execution failed with exit code {result.ExitCode}. stderr: {result.StandardError.Trim()} stdout: {result.StandardOutput.Trim()}");
            }

            return OpenAiCompatiblePhaseExecutionProvider.ParseClaudeJsonExecutionResult(result.StandardOutput);
        }
    }

    internal sealed class SystemCopilotCliRunner : NativeCliRunnerBase
    {
        public override string ProviderKind => "copilot";

        protected override string ExecutablePathEnvVar => "SPECFORGE_COPILOT_CLI_PATH";

        protected override string[] CandidateExecutableNames => ["gh"];

        protected override IReadOnlyList<string> GetVersionArguments() => ["copilot", "--", "--version"];

        public override async Task<NativeCliExecutionResult> ExecuteAsync(NativeCliInvocation invocation, CancellationToken cancellationToken)
        {
            var arguments = new List<string>
            {
                "copilot",
                "-p",
                invocation.Prompt
            };

            var result = await RunProcessAsync(arguments, invocation.WorkspaceRoot, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Copilot CLI execution failed with exit code {result.ExitCode}. stderr: {result.StandardError.Trim()} stdout: {result.StandardOutput.Trim()}");
            }

            return new NativeCliExecutionResult(result.StandardOutput.Trim(), null);
        }
    }

    internal interface INativeCliRunner
    {
        string ProviderKind { get; }
        bool IsAvailable { get; }
        Task<NativeCliCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken);
        Task<NativeCliExecutionResult> ExecuteAsync(NativeCliInvocation invocation, CancellationToken cancellationToken);
    }

    internal abstract class NativeCliRunnerBase : INativeCliRunner
    {
        private readonly string? executablePath;

        protected NativeCliRunnerBase()
        {
            executablePath = ResolveExecutablePath();
        }

        public abstract string ProviderKind { get; }
        public bool IsAvailable => !string.IsNullOrWhiteSpace(executablePath);
        protected string ExecutablePath => executablePath ?? throw new InvalidOperationException($"{ProviderKind} CLI executable could not be resolved.");
        protected abstract string ExecutablePathEnvVar { get; }
        protected abstract string[] CandidateExecutableNames { get; }
        protected virtual string? BundledExecutablePath => null;
        protected abstract IReadOnlyList<string> GetVersionArguments();

        public async Task<NativeCliCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken)
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException($"{ProviderKind} CLI executable could not be resolved.");
            }

            var result = await RunProcessAsync(GetVersionArguments(), Environment.CurrentDirectory, cancellationToken);
            return new NativeCliCheckResult(result.Command, result.ExitCode, result.StandardOutput, result.StandardError);
        }

        public abstract Task<NativeCliExecutionResult> ExecuteAsync(NativeCliInvocation invocation, CancellationToken cancellationToken);

        protected async Task<ProcessExecutionResult> RunProcessAsync(
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            string? standardInput = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = standardInput is not null,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var command = BuildSanitizedCommandForLog(ExecutablePath, arguments, standardInput);
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            SpecForgeDiagnostics.Log($"[provider.native.exec] provider={ProviderKind} command=\"{command}\" pid={process.Id} started.");
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
            });

            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput);
                await process.StandardInput.FlushAsync();
                process.StandardInput.Close();
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            stdout.Append(await stdoutTask);
            stderr.Append(await stderrTask);

            SpecForgeDiagnostics.Log(
                $"[provider.native.exec] provider={ProviderKind} command=\"{command}\" pid={process.Id} exitCode={process.ExitCode} stdout={FormatProcessOutputForLog(stdout.ToString())} stderr={FormatProcessOutputForLog(stderr.ToString())}");

            return new ProcessExecutionResult(command, process.ExitCode, stdout.ToString(), stderr.ToString());
        }

        private string? ResolveExecutablePath()
        {
            var explicitPath = Environment.GetEnvironmentVariable(ExecutablePathEnvVar);
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            if (!string.IsNullOrWhiteSpace(BundledExecutablePath) && File.Exists(BundledExecutablePath))
            {
                return BundledExecutablePath;
            }

            var currentPath = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return null;
            }

            foreach (var candidateDirectory in currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                foreach (var executableName in CandidateExecutableNames)
                {
                    var candidatePath = Path.Combine(candidateDirectory, executableName);
                    if (File.Exists(candidatePath))
                    {
                        return candidatePath;
                    }
                }
            }

            return null;
        }
    }
}

internal sealed record NativeCliInvocation(
    string ProviderKind,
    string WorkspaceRoot,
    string Prompt,
    string? Model,
    string? ReasoningEffort,
    string SandboxMode);

internal sealed record NativeCliCheckResult(
    string Command,
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal sealed record NativeCliExecutionResult(
    string Content,
    TokenUsage? Usage);

internal sealed record ProcessExecutionResult(
    string Command,
    int ExitCode,
    string StandardOutput,
    string StandardError);
