using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SiteAdaptation.Common;

namespace SiteAdaptation.AddIn.Flow
{
    // R382, R347. What a body sees of its run. Every file it writes goes to <name>.tmp-<run id> first: in a dry run under the dry
    // folder, in the real run beside its name, moved onto the name after the commit
    public sealed class RunContext
    {
        private readonly List<KeyValuePair<string, string>> outputs = new List<KeyValuePair<string, string>>();
        private readonly bool writable;

        internal RunContext(int button, string label, string runId, string workFolder, string dryFolder, bool writable, IReadOnlyList<RunInput> inputs, IReadOnlyDictionary<string, string> inputHashes)
        {
            Button = button;
            Label = label;
            RunId = runId;
            WorkFolder = workFolder;
            DryFolder = dryFolder;
            this.writable = writable;
            Inputs = inputs;
            InputHashes = inputHashes;
        }

        public int Button { get; }
        public string Label { get; }
        public string RunId { get; }
        public string WorkFolder { get; }

        // Null outside a dry run
        public string DryFolder { get; }
        public IReadOnlyList<RunInput> Inputs { get; }
        public IReadOnlyDictionary<string, string> InputHashes { get; }

        public bool DryRun
        {
            get { return DryFolder != null; }
        }

        // Each temporary name with the name it moves onto
        internal IReadOnlyList<KeyValuePair<string, string>> Outputs
        {
            get { return outputs; }
        }

        // The temporary path to write an output to. A relative name lies in the work folder. The count before Apply with no dry run
        // writes nothing, so it gets no path
        public string Output(string path)
        {
            if (!writable)
            {
                throw new InvalidOperationException("a file is written only in a dry run or inside the transaction of the real run");
            }
            string target = Path.IsPathRooted(path) ? path : Path.Combine(WorkFolder, path);
            if (DryRun)
            {
                target = Path.Combine(DryFolder, Path.GetFileName(target));
            }
            string temporary = WorkFiles.TemporaryName(target, RunId);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(temporary)));
            outputs.Add(new KeyValuePair<string, string>(temporary, target));
            return temporary;
        }

        public void WriteText(string path, string text)
        {
            File.WriteAllText(Output(path), text, new UTF8Encoding(false));
        }

        // After a roll back or a failed dry run no partial file is left to be taken for an output
        internal void DeleteOutputs()
        {
            foreach (KeyValuePair<string, string> output in outputs)
            {
                try
                {
                    File.Delete(output.Key);
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    // A name holding .tmp- is never read, and a later run of R347 deletes it
                }
            }
        }
    }
}
