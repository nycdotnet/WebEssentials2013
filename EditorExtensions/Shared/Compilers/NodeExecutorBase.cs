﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MadsKristensen.EditorExtensions.JavaScript;
using MadsKristensen.EditorExtensions.Settings;

namespace MadsKristensen.EditorExtensions
{
    public abstract class NodeExecutorBase
    {
        ///<summary>Indicates whether this compiler will emit a source map file.  Will only return true if supported and enabled in user settings.</summary>
        public abstract bool MinifyInPlace { get; }
        public abstract bool GenerateSourceMap { get; }
        public abstract string TargetExtension { get; }
        public abstract string ServiceName { get; }

        public async Task<CompilerResult> CompileAsync(string sourceFileName, string targetFileName)
        {
            if (WEIgnore.TestWEIgnore(sourceFileName, this is ILintCompiler ? "linter" : "compiler", ServiceName.ToLowerInvariant()))
            {
                Logger.Log(String.Format(CultureInfo.CurrentCulture, "{0}: The file {1} is ignored by .weignore. Skipping..", ServiceName, Path.GetFileName(sourceFileName)));
                return await CompilerResultFactory.GenerateResult(sourceFileName, targetFileName, string.Empty, false, string.Empty, string.Empty, Enumerable.Empty<CompilerError>(), true);
            }

            CompilerResult response = await NodeServer.CallServiceAsync(GetPath(sourceFileName, targetFileName));

            return await ProcessResult(response, sourceFileName, targetFileName);
        }

        // Don't try-catch this method: We need to "address" all the bugs,
        // which may occur as the (node.js-based) service implement changes.
        private async Task<CompilerResult> ProcessResult(CompilerResult result, string sourceFileName, string targetFileName)
        {
            if (result == null)
            {
                Logger.Log(ServiceName + ": " + Path.GetFileName(sourceFileName) + " compilation failed: The service failed to respond to this request\n\t\t\tPossible cause: Syntax Error!");
                return await CompilerResultFactory.GenerateResult(sourceFileName, targetFileName);
            }
            if (!result.IsSuccess)
            {
                var firstError = result.Errors.Where(e => e != null).Select(e => e.Message).FirstOrDefault();

                if (firstError != null)
                    Logger.Log(ServiceName + ": " + Path.GetFileName(result.SourceFileName) + " compilation failed: " + firstError);

                return result;
            }

            string resultString = PostProcessResult(result);

            if (result.TargetFileName != null && (MinifyInPlace || !File.Exists(result.TargetFileName) ||
                !ReferenceEquals(string.Intern(resultString), string.Intern(await FileHelpers.ReadAllTextRetry(result.TargetFileName)))))
            {
                ProjectHelpers.CheckOutFileFromSourceControl(result.TargetFileName);
                await FileHelpers.WriteAllTextRetry(result.TargetFileName, resultString);
            }
            if (GenerateSourceMap && (!File.Exists(result.MapFileName) ||
                !ReferenceEquals(string.Intern(result.ResultMap), string.Intern(await FileHelpers.ReadAllTextRetry(result.MapFileName)))))
            {
                ProjectHelpers.CheckOutFileFromSourceControl(result.MapFileName);
                await FileHelpers.WriteAllTextRetry(result.MapFileName, result.ResultMap);
            }

            return CompilerResult.UpdateResult(result, resultString);
        }

        protected virtual string PostProcessResult(CompilerResult result)
        {
            Logger.Log(ServiceName + ": " + Path.GetFileName(result.SourceFileName) + " compiled.");
            return result.Result;
        }

        protected abstract string GetPath(string sourceFileName, string targetFileName);
    }
}
