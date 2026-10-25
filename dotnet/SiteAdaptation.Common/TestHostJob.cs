using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SiteAdaptation.Common
{
    // testhost_job.json as the run script writes it (R103)
    public class TestHostJob
    {
        // place (the default) runs the placer and the reader on a model copy, upgrade saves one prototype as a Revit 2024 file (R109).
        // master makes the start file of the floor tests, floors and linksAndFloors run the floor step on a work file (R117)
        public string Kind { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public string Token { get; set; }
        public string Code { get; set; }
        public string Model { get; set; }
        public string PrototypeFile { get; set; }
        public string Plan { get; set; }
        public DateTime? CreatedUtc { get; set; }
        // Minutes for the whole job, counted from the claim. Null takes TestHostGuard.DefaultTotalMinutes
        public double? TotalMinutes { get; set; }
        // R117. all deletes every floor of the types of floorSettings before the step, plots only those of the plots in DeletePlots
        public string DeleteFloors { get; set; }
        public List<string> DeletePlots { get; set; }
        // The plot numbers the floor step tries. Null is every plot
        public List<string> Plots { get; set; }
        public bool? DryRunOnly { get; set; }
        public bool? RunTwice { get; set; }
        // stop or rollBack when the scenario wants the floor step to end that way, else the step must place cleanly
        public string Expect { get; set; }
        // R212. The template a newSite job starts from and the site file of SiteSpec it sets up the new file with
        public string Template { get; set; }
        public string Site { get; set; }
        // R214. The folder an image job writes to, the level of its plan view and the area [minE, minN, maxE, maxN] in shared metres
        public string ImageFolder { get; set; }
        public string ImageLevel { get; set; }
        public List<double> Area { get; set; }
        // R234. An image job may load every Revit link first, from LinkFolder by file name when its saved path is not there
        public bool? LoadLinks { get; set; }
        public string LinkFolder { get; set; }
        // R302. The most the image step may run, testHost.imageStepMinutes of config/project.json as the run script writes it
        public double? ImageMinutes { get; set; }
    }

    public sealed class GuardResult
    {
        public bool Act { get; set; }
        public string Reason { get; set; }
        public TestHostJob Job { get; set; }
    }

    // R103. The test host acts only on a job that passes every check here, and never in a Revit session the run script did not start
    public static class TestHostGuard
    {
        // R110. Each Revit the run script starts gets the full path of its own job file in JobVariable and its own token in TokenVariable
        public const string JobVariable = "SITEADAPT_JOB";
        public const string TokenVariable = "SITEADAPTATION_TESTHOST_TOKEN";
        public const int MinTokenLength = 32;
        public const double MaxAgeMinutes = 60;
        public const double MaxClockSkewMinutes = 5;
        public const double DefaultTotalMinutes = 27;
        public const double MaxTotalMinutes = 29;
        public const string TestTag = "_TEST-";
        public const string PlaceKind = "place";
        public const string UpgradeKind = "upgrade";
        public const string MasterKind = "master";
        public const string FloorsKind = "floors";
        public const string LinksAndFloorsKind = "linksAndFloors";
        // R204, R205, R212, R214. A site model saved as a new Revit 2024 test copy, a read of a test copy, a new site file
        // made from a template, and a plan view image of a test copy or a new site file
        public const string SiteUpgradeKind = "siteUpgrade";
        public const string ReadKind = "read";
        public const string NewSiteKind = "newSite";
        public const string ImageKind = "image";
        // R117. The one master file and the work files of the floor scenarios, named after the site model
        public const string MasterCode = "MST";
        public const string MasterTag = "_TEST-MASTER";
        public const string WorkTag = "_TEST-WORK-";
        // R213. The one name a new site file may have. Only the newSite job makes it, and only where nothing of that name is
        public const string NewSiteTag = "-TEST-NEW";
        public const int MaxPlots = 1000;
        private static readonly string[] Kinds = { PlaceKind, UpgradeKind, MasterKind, FloorsKind, LinksAndFloorsKind, SiteUpgradeKind, ReadKind, NewSiteKind, ImageKind };
        private static readonly string[] Deletes = { "all", "plots" };
        private static readonly string[] Expects = { "stop", "rollBack" };
        private static readonly Regex WorkCode = new Regex(@"\AS[1-9][0-9]{0,2}\z", RegexOptions.CultureInvariant);
        private static readonly Regex PlotText = new Regex(@"\A[0-9]{1,6}\z", RegexOptions.CultureInvariant);

        private static readonly Regex CodeText = new Regex(@"\A[A-Z][A-Z0-9]{1,3}\z", RegexOptions.CultureInvariant);
        private static readonly Regex TokenText = new Regex(@"\A[A-Za-z0-9]+\z", RegexOptions.CultureInvariant);

        public static string SiteAdaptationFolder(string documents)
        {
            return Path.Combine(documents, "SiteAdaptation");
        }

        private static readonly Regex JobName = new Regex(@"\Atesthost_job_(?<code>[A-Z][A-Z0-9]{1,3})(_upgrade)?\.json\z", RegexOptions.CultureInvariant);

        // R110. The job file of one window: testhost_job_<code>.json, or testhost_job_<code>_upgrade.json for an upgrade
        public static string JobPath(string documents, string code, bool upgrade)
        {
            return Path.Combine(SiteAdaptationFolder(documents), "testhost_job_" + code + (upgrade ? "_upgrade" : "") + ".json");
        }

        // The code a job path names, or null when the path is not a job file of the SiteAdaptation folder
        public static string CodeOfJobPath(string path, string documents)
        {
            if (!IsPlainFullPath(path) || !string.Equals(Path.GetDirectoryName(path), SiteAdaptationFolder(documents), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            Match name = JobName.Match(Path.GetFileName(path));
            return name.Success ? name.Groups["code"].Value : null;
        }

        // R110. Null when the job sits in the file of its own code and kind, else why not
        public static string CheckJobPath(string path, TestHostJob job, string documents)
        {
            string code = CodeOfJobPath(path, documents);
            if (code == null)
            {
                return "the job file is not testhost_job_<code>.json in " + SiteAdaptationFolder(documents);
            }
            if (job == null || job.Code != code)
            {
                return "the job file names " + code + " and the job another code";
            }
            bool upgradeFile = Path.GetFileName(path).EndsWith("_upgrade.json", StringComparison.Ordinal);
            if (upgradeFile != IsUpgrade(job))
            {
                return "the job kind does not fit its file name";
            }
            return null;
        }

        // Where a refusal of this job is written, next to its job file, so two windows never write one file
        public static string RefusedPath(string jobPath)
        {
            return Path.ChangeExtension(jobPath, ".refused.json");
        }

        // The cheap first look at Revit start. Without the token, the job variable and its file, the test host subscribes to nothing and touches no file
        public static bool ShouldWake(string token, string jobPath, bool jobFileExists)
        {
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(jobPath) && jobFileExists;
        }

        // Every check of R103 on the text of the job file. fileExists and folderExists are asked only for full paths
        public static GuardResult Check(string jobText, string token, DateTime nowUtc, Func<string, bool> fileExists, Func<string, bool> folderExists = null)
        {
            try
            {
                return CheckJob(jobText, token, nowUtc, fileExists, folderExists ?? Directory.Exists);
            }
            catch (Exception failure)
            {
                // A check can refuse and never throw, so Revit never sees an add-in failure from it
                return Refuse("the job could not be checked: " + failure.GetType().Name + ": " + failure.Message);
            }
        }

        // A drive path or a share that Windows would not change: no stream or device colon, no \\?\ or \\.\ prefix,
        // no parent step and no trailing dot or space, so the name checked is the file opened
        public static bool IsPlainFullPath(string path)
        {
            if (!Placer.IsFullPath(path) || HasParentStep(path) || path.IndexOf(':', 2) >= 0 || path.IndexOf('/') >= 0)
            {
                return false;
            }
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                return false;
            }
            return string.Equals(Path.GetFullPath(path), path, StringComparison.Ordinal);
        }

        private static GuardResult CheckJob(string jobText, string token, DateTime nowUtc, Func<string, bool> fileExists, Func<string, bool> folderExists)
        {
            if (string.IsNullOrEmpty(token) || token.Length < MinTokenLength || !TokenText.IsMatch(token))
            {
                return Refuse("the token in " + TokenVariable + " is missing or shorter than " + MinTokenLength + " letters and digits");
            }
            if (string.IsNullOrWhiteSpace(jobText))
            {
                return Refuse("the job file is empty");
            }
            TestHostJob job;
            try
            {
                job = JsonConvert.DeserializeObject<TestHostJob>(jobText, new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc });
            }
            catch (JsonException failure)
            {
                return Refuse("the job file does not read: " + failure.Message);
            }
            if (job == null)
            {
                return Refuse("the job file holds no job");
            }
            // Ordinal on equal length, a token from an older job or another session never matches
            if (!string.Equals(job.Token, token, StringComparison.Ordinal))
            {
                return Refuse("the job token is not the token of this Revit session");
            }
            if (!job.CreatedUtc.HasValue)
            {
                return Refuse("the job has no createdUtc");
            }
            double age = (nowUtc - job.CreatedUtc.Value.ToUniversalTime()).TotalMinutes;
            if (age > MaxAgeMinutes || age < -MaxClockSkewMinutes)
            {
                return Refuse("the job was written " + age.ToString("0.#", CultureInfo.InvariantCulture) + " minutes ago, only a job younger than " + MaxAgeMinutes + " minutes is run");
            }
            if (job.Code == null || !CodeText.IsMatch(job.Code))
            {
                return Refuse("the job code is not a prototype code such as C10 or TH1");
            }
            if (job.Kind != null && !Kinds.Contains(job.Kind))
            {
                return Refuse("the job kind is not " + string.Join(", ", Kinds));
            }
            string prototype = CheckPrototype(job);
            if (prototype != null)
            {
                return Refuse(prototype);
            }
            // Written so that NaN fails too. R302 an image job may add its image minutes to the cap
            double totalCap = MaxTotalMinutes + (job.Kind == ImageKind && HasImageMinutes(job) ? job.ImageMinutes.Value : 0);
            if (job.TotalMinutes.HasValue && !(job.TotalMinutes.Value > 0 && job.TotalMinutes.Value <= totalCap))
            {
                return Refuse("totalMinutes must be above 0 and at most " + totalCap);
            }
            string extras = CheckExtras(job);
            if (extras != null)
            {
                return Refuse(extras);
            }
            if (job.Kind == UpgradeKind)
            {
                return CheckUpgrade(job, fileExists, folderExists);
            }
            if (job.Kind == SiteUpgradeKind)
            {
                return CheckSiteUpgrade(job, fileExists);
            }
            if (job.Source != null || job.Target != null)
            {
                return Refuse("a " + (job.Kind ?? PlaceKind) + " job names no source and no target");
            }
            string options = CheckFloorOptions(job);
            if (options != null)
            {
                return Refuse(options);
            }
            string model = CheckModel(job);
            if (model != null)
            {
                return Refuse(model);
            }
            if (job.Kind == NewSiteKind)
            {
                return CheckNewSite(job, fileExists, folderExists);
            }
            if (job.Kind == ReadKind || job.Kind == ImageKind)
            {
                return CheckLook(job, fileExists, folderExists);
            }
            if (!IsPlainFullPath(job.Plan) || !job.Plan.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("the plan is not a full path to a .json file");
            }
            if (!fileExists(job.Model))
            {
                return Refuse("the model copy does not exist: " + job.Model);
            }
            if (!fileExists(job.Plan))
            {
                return Refuse("the plan does not exist: " + job.Plan);
            }
            return new GuardResult { Act = true, Job = job };
        }

        // R109. The source is only read. The target is a new file of the same name in another folder that exists, and it must not exist yet
        private static GuardResult CheckUpgrade(TestHostJob job, Func<string, bool> fileExists, Func<string, bool> folderExists)
        {
            if (job.Model != null || job.Plan != null)
            {
                return Refuse("an upgrade job names no model and no plan");
            }
            if (!IsPlainFullPath(job.Source) || Path.GetFileName(job.Source) != job.PrototypeFile)
            {
                return Refuse("the source is not a plain full path to the prototype file");
            }
            if (!IsPlainFullPath(job.Target) || Path.GetFileName(job.Target) != job.PrototypeFile)
            {
                return Refuse("the target is not a plain full path with the same file name");
            }
            string sourceFolder = Path.GetDirectoryName(job.Source);
            string targetFolder = Path.GetDirectoryName(job.Target);
            if (string.Equals(sourceFolder, targetFolder, StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("the target folder is the source folder, the source is never overwritten");
            }
            if (!fileExists(job.Source))
            {
                return Refuse("the source does not exist: " + job.Source);
            }
            if (fileExists(job.Target))
            {
                return Refuse("the target already exists, it is never overwritten: " + job.Target);
            }
            if (!folderExists(targetFolder))
            {
                return Refuse("the target folder does not exist: " + targetFolder);
            }
            return new GuardResult { Act = true, Job = job };
        }

        // R204. A site model becomes <its name>_TEST-<code>.rvt next to it, a new file it never was. The source is only read
        private static GuardResult CheckSiteUpgrade(TestHostJob job, Func<string, bool> fileExists)
        {
            if (job.Model != null || job.Plan != null)
            {
                return Refuse("a site upgrade job names no model and no plan");
            }
            if (!IsPlainFullPath(job.Source) || !job.Source.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("the source is not a plain full path to a .rvt file");
            }
            string sourceName = Path.GetFileName(job.Source);
            if (sourceName.IndexOf(TestTag, StringComparison.OrdinalIgnoreCase) >= 0 || IsNewSiteName(sourceName))
            {
                return Refuse("the source is a test copy or a new site file, a site upgrade starts from a copy of a site model");
            }
            string wanted = Path.Combine(Path.GetDirectoryName(job.Source), Path.GetFileNameWithoutExtension(job.Source) + TestTag + job.Code + ".rvt");
            if (!IsPlainFullPath(job.Target) || !string.Equals(job.Target, wanted, StringComparison.Ordinal))
            {
                return Refuse("the target is not " + wanted);
            }
            if (!fileExists(job.Source))
            {
                return Refuse("the source does not exist: " + job.Source);
            }
            if (fileExists(job.Target))
            {
                return Refuse("the target already exists, it is never overwritten: " + job.Target);
            }
            return new GuardResult { Act = true, Job = job };
        }

        // R212. The new file must not be there yet, the template and the site file must
        private static GuardResult CheckNewSite(TestHostJob job, Func<string, bool> fileExists, Func<string, bool> folderExists)
        {
            if (job.Plan != null)
            {
                return Refuse("a newSite job names no plan");
            }
            if (!IsPlainFullPath(job.Template) || !job.Template.EndsWith(".rte", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("the template is not a plain full path to a .rte file");
            }
            if (!IsPlainFullPath(job.Site) || !job.Site.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("the site file is not a plain full path to a .json file");
            }
            if (!fileExists(job.Template))
            {
                return Refuse("the template does not exist: " + job.Template);
            }
            if (!fileExists(job.Site))
            {
                return Refuse("the site file does not exist: " + job.Site);
            }
            if (fileExists(job.Model))
            {
                return Refuse("the new site file already exists, it is never overwritten: " + job.Model);
            }
            if (!folderExists(Path.GetDirectoryName(job.Model)))
            {
                return Refuse("the folder of the new site file does not exist");
            }
            return new GuardResult { Act = true, Job = job };
        }

        // R205, R214. A read or an image looks at a model and never saves it, so it names no plan
        private static GuardResult CheckLook(TestHostJob job, Func<string, bool> fileExists, Func<string, bool> folderExists)
        {
            if (job.Plan != null)
            {
                return Refuse("a " + job.Kind + " job names no plan");
            }
            if (job.Kind == ImageKind)
            {
                if (!IsPlainFullPath(job.ImageFolder) || !folderExists(job.ImageFolder))
                {
                    return Refuse("the image folder is not a plain full path to a folder that exists");
                }
                if (string.IsNullOrWhiteSpace(job.ImageLevel) || job.ImageLevel.Length > 100)
                {
                    return Refuse("the image level is not a level name");
                }
                List<double> area = job.Area;
                // Written so that NaN fails too
                if (area == null || area.Count != 4 || !area.All(value => value > -1e8 && value < 1e8)
                    || !(area[2] - area[0] > 0 && area[2] - area[0] <= MaxImageSpanM) || !(area[3] - area[1] > 0 && area[3] - area[1] <= MaxImageSpanM))
                {
                    return Refuse("the area is not [minE, minN, maxE, maxN] with each side above 0 and at most " + MaxImageSpanM + " m");
                }
                if (job.LinkFolder != null && (!IsPlainFullPath(job.LinkFolder) || !folderExists(job.LinkFolder)))
                {
                    return Refuse("the link folder is not a plain full path to a folder that exists");
                }
                if (!HasImageMinutes(job))
                {
                    return Refuse("an image job names imageMinutes above 0 and at most " + MaxImageMinutes + ", testHost.imageStepMinutes of config/project.json");
                }
            }
            if (!fileExists(job.Model))
            {
                return Refuse("the model copy does not exist: " + job.Model);
            }
            return new GuardResult { Act = true, Job = job };
        }

        public const double MaxImageSpanM = 5000;
        // R302. Only a bound of the guard against a mistyped job, the limit itself is testHost.imageStepMinutes of the config
        public const double MaxImageMinutes = 120;

        // Written so that NaN fails too
        private static bool HasImageMinutes(TestHostJob job)
        {
            return job.ImageMinutes.HasValue && job.ImageMinutes.Value > 0 && job.ImageMinutes.Value <= MaxImageMinutes;
        }

        // R302. The limit of the image step, from the job and never a constant. The guard has refused an image job without it
        public static double ImageStepMinutes(TestHostJob job)
        {
            if (job == null || job.Kind != ImageKind || !HasImageMinutes(job))
            {
                throw new InvalidOperationException("only an image job with imageMinutes has an image step");
            }
            return job.ImageMinutes.Value;
        }

        // The fields of the newSite and image jobs belong to those jobs alone
        private static string CheckExtras(TestHostJob job)
        {
            if (job.Kind != NewSiteKind && (job.Template != null || job.Site != null))
            {
                return "template and site go with a newSite job only";
            }
            if (job.Kind != ImageKind && (job.ImageFolder != null || job.ImageLevel != null || job.Area != null || job.LoadLinks.HasValue || job.LinkFolder != null || job.ImageMinutes.HasValue))
            {
                return "imageFolder, imageLevel, area, loadLinks, linkFolder and imageMinutes go with an image job only";
            }
            if (job.LinkFolder != null && job.LoadLinks != true)
            {
                return "linkFolder goes with loadLinks";
            }
            return null;
        }

        public static bool IsUpgrade(TestHostJob job)
        {
            return job != null && (job.Kind == UpgradeKind || job.Kind == SiteUpgradeKind);
        }

        // R213. <name>-TEST-NEW.rvt with the tag once and no _TEST- in it, so it is never mistaken for a copy of a site model
        public static bool IsNewSiteName(string name)
        {
            string tail = NewSiteTag + ".rvt";
            if (name == null || name.Length <= tail.Length || !name.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return name.IndexOf(NewSiteTag, StringComparison.OrdinalIgnoreCase) == name.Length - tail.Length
                && name.IndexOf(TestTag, StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static bool IsNewSite(string model)
        {
            return IsPlainFullPath(model) && IsNewSiteName(Path.GetFileName(model));
        }

        // The links steps need the prototype file of the code, the master and floors jobs name none, nor do the jobs of R204 to R214
        private static string CheckPrototype(TestHostJob job)
        {
            if (job.Kind == MasterKind || job.Kind == FloorsKind || job.Kind == SiteUpgradeKind || job.Kind == ReadKind || job.Kind == NewSiteKind || job.Kind == ImageKind)
            {
                return job.PrototypeFile == null ? null : "a " + job.Kind + " job names no prototype file";
            }
            string code = job.Kind == LinksAndFloorsKind ? @"[A-Z][A-Z0-9]{1,3}" : Regex.Escape(job.Code);
            bool fits = new Regex(@"\ARRE-WP-NSP-KEO-ARC-" + code + @"-ZZ-MOD-\d+\.rvt\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).IsMatch(job.PrototypeFile ?? "");
            return fits ? null : "the prototype file is not RRE-WP-NSP-KEO-ARC-" + (job.Kind == LinksAndFloorsKind ? "<code>" : job.Code) + "-ZZ-MOD-<n>.rvt";
        }

        public static bool PlacesFloors(TestHostJob job)
        {
            return job.Kind == FloorsKind || job.Kind == LinksAndFloorsKind;
        }

        // R117. The options of the floor scenarios, each only where it means something
        private static string CheckFloorOptions(TestHostJob job)
        {
            bool floors = PlacesFloors(job);
            if (!floors && (job.Plots != null || job.DeletePlots != null || job.DryRunOnly.HasValue || job.RunTwice.HasValue || job.Expect != null))
            {
                return "plots, deletePlots, dryRunOnly, runTwice and expect go with a floors or linksAndFloors job only";
            }
            if (job.Kind == MasterKind ? job.DeleteFloors != "all" : !floors && job.DeleteFloors != null)
            {
                return job.Kind == MasterKind ? "a master job deletes all floors, deleteFloors is all" : "deleteFloors goes with a master, floors or linksAndFloors job only";
            }
            if (job.DeleteFloors != null && !Deletes.Contains(job.DeleteFloors))
            {
                return "deleteFloors is all or plots";
            }
            foreach (List<string> plots in new[] { job.Plots, job.DeletePlots })
            {
                if (plots != null && (plots.Count == 0 || plots.Count > MaxPlots || plots.Any(plot => plot == null || !PlotText.IsMatch(plot)) || plots.Distinct().Count() != plots.Count))
                {
                    return "plots and deletePlots are lists of 1 to " + MaxPlots + " different plot numbers in digits";
                }
            }
            if ((job.DeleteFloors == "plots") != (job.DeletePlots != null))
            {
                return "deleteFloors plots goes with deletePlots and nothing else does";
            }
            if (job.DryRunOnly == true && job.RunTwice == true)
            {
                return "a dry run only job does not run twice";
            }
            if (job.Expect != null && !Expects.Contains(job.Expect))
            {
                return "expect is stop or rollBack";
            }
            if (job.Kind == MasterKind && job.Code != MasterCode)
            {
                return "the master job has the code " + MasterCode;
            }
            if (floors && !WorkCode.IsMatch(job.Code))
            {
                return "a floors job has a scenario code S1 to S999";
            }
            return null;
        }

        // Only a copy named <name>_TEST-<code>.rvt is ever opened, saved or changed. The master is <name>_TEST-MASTER.rvt
        // and the work file of a floor scenario <name>_TEST-WORK-S<n>.rvt (R117)
        private static string CheckModel(TestHostJob job)
        {
            if (!IsPlainFullPath(job.Model))
            {
                return "the model is not a full path";
            }
            string name = Path.GetFileName(job.Model);
            // R213. Only the newSite job makes a new site file, and only the floor steps and the image may open one after it
            if (job.Kind == NewSiteKind)
            {
                return IsNewSiteName(name) ? null : "a newSite job makes a file named <name>" + NewSiteTag + ".rvt";
            }
            if (IsNewSiteName(name) && (PlacesFloors(job) || job.Kind == ImageKind))
            {
                return null;
            }
            string tail = (job.Kind == MasterKind ? MasterTag : PlacesFloors(job) ? WorkTag + job.Code : TestTag + job.Code) + ".rvt";
            if (!name.EndsWith(tail, StringComparison.OrdinalIgnoreCase) || name.Length <= tail.Length)
            {
                return "the model is not a copy named <name>" + tail;
            }
            if (name.IndexOf(TestTag, StringComparison.OrdinalIgnoreCase) != name.Length - tail.Length)
            {
                return "the model name holds " + TestTag + " more than once";
            }
            return null;
        }

        private static bool HasParentStep(string path)
        {
            foreach (string part in path.Split('\\', '/'))
            {
                if (part == "..")
                {
                    return true;
                }
            }
            return false;
        }

        // R117. The master next to a work file. A work file must be a copy of its own and never the master itself
        public static string MasterOf(string model)
        {
            string name = Path.GetFileName(model);
            int at = name.LastIndexOf(TestTag, StringComparison.OrdinalIgnoreCase);
            return at <= 0 ? null : Path.Combine(Path.GetDirectoryName(model), name.Substring(0, at) + MasterTag + ".rvt");
        }

        // The file the original copy came from, which the test host must never open
        public static string OriginalOf(string model)
        {
            string name = Path.GetFileName(model);
            int at = name.LastIndexOf(TestTag, StringComparison.OrdinalIgnoreCase);
            return at <= 0 ? null : Path.Combine(Path.GetDirectoryName(model), name.Substring(0, at) + ".rvt");
        }

        // R104. The outputs of one job. The reader output is never prototypes.json
        public static string OutputPath(string documents, string code, string part)
        {
            string name = "testhost_" + code + "_" + part + ".json";
            if (string.Equals(name, "prototypes.json", StringComparison.OrdinalIgnoreCase) || name.StartsWith("testhost_job", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("the test host never writes " + name);
            }
            return Path.Combine(SiteAdaptationFolder(documents), name);
        }

        private static GuardResult Refuse(string reason)
        {
            return new GuardResult { Act = false, Reason = reason };
        }
    }
}
