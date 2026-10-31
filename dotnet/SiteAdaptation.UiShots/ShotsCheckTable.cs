using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using SiteAdaptation.AddIn.CheckTable;
using SiteAdaptation.Common;
using SiteAdaptation.Common.CheckTable;
using SiteAdaptation.Common.ManualRules;
using SiteAdaptation.Common.Review;

namespace SiteAdaptation.UiShots
{
    // R376. The check table window of button 4 for the picture test, with made up numbers only
    public static partial class Shots
    {
        private static BitmapSource CheckPage()
        {
            CheckTableWindow window = LaidOutCheck(SampleCheckView(false));
            FrameworkElement body = Body(window);
            var bitmap = new RenderTargetBitmap((int)body.ActualWidth, (int)body.ActualHeight, Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(body);
            bitmap.Freeze();
            return bitmap;
        }

        // The check table window on a view, laid out as the others and not shown, the first row selected so the side box is filled
        public static CheckTableWindow LaidOutCheck(CheckTableView view)
        {
            var session = new CheckTableSession(view, new CheckTableCallbacks { Save = choices => "list.json" }, SampleRuleLine(), id => { });
            if (session.Model.Rows.Count > 0)
            {
                session.Model.Selected = session.Model.Rows[0];
            }
            var window = new CheckTableWindow(session);
            LayOut(window);
            return window;
        }

        // The rule lines of the block manualRules of the rules profile of the repo
        public static Func<string, string> SampleRuleLine()
        {
            ManualRuleBlock block = ManualRuleBlock.FromFile(RulesFile());
            return id => block.Line(id);
        }

        // Found by walking up from the folder of the program to the folder that holds rules/roshn-makkah.json
        public static string RulesFile()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "rules", "roshn-makkah.json")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            if (folder == null)
            {
                throw new FileNotFoundException("rules/roshn-makkah.json was not found above " + AppDomain.CurrentDomain.BaseDirectory);
            }
            return Path.Combine(folder, "rules", "roshn-makkah.json");
        }

        // Made up numbers only, no coordinate and no level of a real model. Five plots of a made up NBH 012 with a past job of one key,
        // the manual rule ids by the profile of the repo. dryRun true is the table of a dry run, Save overrides grey
        public static CheckTableView SampleCheckView(bool dryRun)
        {
            JObject step = Wrapped(JObject.Parse(@"{
  'plots': {
    '31': { 'unit': 'VL4', 'nbh': '012', 'rows': [
      { 'check': 'Level', 'field': 'heightM', 'from': 'override', 'use': 'Past', 'rule': 'ffl', 'toolValue': 'FFL by the rule', 'past': { 'writes': 'gapToRuleM', 'value': { 'gapToRuleM': 0.001 }, 'why': null }, 'why': 'an override of the list' },
      { 'check': 'Level', 'field': 'steps', 'from': 'override', 'use': 'Past', 'rule': 'steps', 'toolValue': 1, 'past': { 'writes': 'steps', 'value': 2, 'why': null }, 'why': 'an override of the list' } ] },
    '32': { 'unit': 'DT1', 'nbh': '012', 'rows': [
      { 'check': 'Hand', 'field': 'mirrored', 'from': 'missing', 'use': 'Rule', 'rule': 'clusterSide', 'toolValue': 'not mirrored', 'past': { 'writes': 'trueOrFalse', 'value': null, 'why': null }, 'why': 'no link' },
      { 'check': 'Floor', 'field': 'yardOutline', 'from': 'override', 'use': 'Rule', 'rule': 'fittedOutlineRefit', 'toolValue': 'fitted yard outline', 'past': { 'writes': null, 'value': null, 'why': 'a build places the outline of its rule' }, 'why': 'the key yard is off it' } ] },
    '33': { 'unit': 'VL6', 'nbh': '012', 'rows': [] },
    '34': { 'unit': 'C10', 'nbh': '012', 'rows': [
      { 'check': 'Position', 'field': 'insert', 'from': 'override', 'use': 'Past', 'rule': 'linkOrigin', 'toolValue': 'the plot frame origin', 'past': { 'writes': 'gapToRuleM', 'value': { 'gapToRuleM': [0.011, 0.001] }, 'why': null }, 'why': 'an override of the list' } ] },
    '35': { 'unit': 'TH2', 'nbh': '012', 'rows': [] }
  },
  'tables': { 'assumed': { 'rampSlope': { 'status': 'CHOSEN' } }, 'chips': { 'All': 5, 'followEveryRule': 2 } }
}"));
            JObject block = JObject.Parse("{ 'types': ['Plot cut', 'Floor', 'Hand', 'Unit code', 'Position', 'Level'], 'fields': { } }");
            var past = new PastJob
            {
                KeyJoin = JObject.Parse("{ 'keys': { 'KX': { 'job': 'kx', 'join': { 'plots': { '33': { 'status': 'cut', 'face': null, 'number': null } } } } } }"),
                Links = new Dictionary<string, KeyLink>
                {
                    ["31"] = new KeyLink { Code = "KX", Model = "kx-site.rvt", File = "P-ARC-VL4-A.rvt", Mirrored = true },
                    ["35"] = new KeyLink { Code = "KX", Model = "kx-site.rvt", File = "P-ARC-VL4-A.rvt", Mirrored = false },
                },
            };
            ManualRuleBlock rules = ManualRuleBlock.FromFile(RulesFile());
            var wrong = new[] { new ReviewCheck { Plot = "35", Check = ReviewCheckValue.Wrong, Note = "front edge on the other side" } };
            CheckTableView view = CheckTableBuild.Compose("012", step, block, wrong, past, rules.IdOfCheck, !dryRun);
            view.Info = new CheckTableInfo { LevelProfile = "parsons2024Rev6", OverrideFile = "roshn-makkah.json", Overrides = 12, OnNbh = 5 };
            view.DoorLevel = SampleDoorLevel();
            return view;
        }

        // R443. A made up config with no civil file and a step levels of the five plots by road level plus slope, so the civil choice is
        // grey with its help line
        public static DoorLevelPick SampleDoorLevel()
        {
            JObject config = JObject.Parse("{ 'designBasis': { 'doorLevel': { 'source': 'roadPlusSlope', 'sources': ['civilFile', 'roadPlusSlope'], 'civilFile': { 'path': null } } } }");
            var plots = new[] { "31", "32", "33", "34", "35" }.ToDictionary(plot => plot, plot => new Dictionary<string, object> { ["doorSource"] = "roadPlusSlope" });
            JObject levels = StepFile.Build(KeyLevels.StepName, "csharp", "nbh=012", plots, new ParityField[0]);
            return DoorLevelPick.Of(config, Path.Combine("made-up", "config", "project.json"), levels, null);
        }

        // The step file the worker writes, each plot as its raw and its written side (R345)
        private static JObject Wrapped(JObject flat)
        {
            var plots = new Dictionary<string, Dictionary<string, object>>();
            foreach (JProperty plot in ((JObject)flat["plots"]).Properties())
            {
                plots[plot.Name] = ((JObject)plot.Value).Properties().ToDictionary(item => item.Name, item => (object)item.Value);
            }
            return StepFile.Build(CheckRows.StepName, "csharp", "nbh=012", plots, new ParityField[0], flat["tables"] as JObject);
        }
    }
}
