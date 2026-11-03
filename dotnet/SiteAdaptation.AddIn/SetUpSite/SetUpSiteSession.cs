using System;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.AddIn.SetUpSite
{
    // R373. What the four buttons of the window do, apart from the window, so a test drives them with no click. A callback that
    // throws leaves the window open with its reason shown and the answer still false
    public sealed class SetUpSiteSession
    {
        private readonly SetUpSiteCallbacks callbacks;
        private readonly Func<string> pickSheet;
        private readonly Func<string> pickPosition;

        public SetUpSiteSession(SetUpSiteView view, SetUpSiteCallbacks callbacks, Func<string> pickSheet, Func<string> pickPosition = null)
        {
            this.pickPosition = pickPosition;
            Model = new SetUpSiteViewModel(view);
            this.callbacks = callbacks ?? new SetUpSiteCallbacks();
            this.pickSheet = pickSheet;
        }

        public SetUpSiteViewModel Model { get; }

        public SetUpSiteAnswer Answer { get; private set; } = new SetUpSiteAnswer { Apply = false };

        // Raised when Apply or Cancel ends the window
        public event Action Finished;

        public void DryRun()
        {
            if (callbacks.DryRun == null)
            {
                return;
            }
            double slope = Model.SelectedSlope;
            Run(() => callbacks.DryRun(slope), slope);
        }

        public void Browse()
        {
            string path = pickSheet?.Invoke();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (callbacks.SheetChosen == null)
            {
                Model.ChosenSheet = path;
                return;
            }
            double slope = Model.SelectedSlope;
            // The path is kept only once its sheet read, so Apply never hands on a sheet the run cannot read
            if (Run(() => callbacks.SheetChosen(path), slope))
            {
                Model.ChosenSheet = path;
            }
        }

        // R513. The three ways to the site position, each read again by Common
        public void UseCad()
        {
            Choose(new SiteSourcePick { Source = SiteSources.Cad });
        }

        public void PickPosition()
        {
            string path = pickPosition?.Invoke();
            if (!string.IsNullOrEmpty(path))
            {
                Choose(new SiteSourcePick { Source = SiteSources.PositionFile, PositionFile = path });
            }
        }

        public void UseTyped()
        {
            Model.ClearProblem();
            if (Model.TypedValues(out double east, out double north, out double angle))
            {
                Choose(new SiteSourcePick { Source = SiteSources.Typed, EastM = east, NorthM = north, AngleDeg = angle });
            }
            else
            {
                Model.Fail(SetUpTexts.TypedNotNumbers);
            }
        }

        private void Choose(SiteSourcePick pick)
        {
            if (callbacks.SourceChosen != null)
            {
                double slope = Model.SelectedSlope;
                Run(() => callbacks.SourceChosen(pick), slope);
            }
        }

        public void Apply()
        {
            if (!Model.CanApply)
            {
                return;
            }
            Answer = new SetUpSiteAnswer { Apply = true, RampSlope = Model.SelectedSlope, ParameterSheet = Model.ChosenSheet, CurrentFiles = Model.TickedInputs, RulesFile = Model.ChosenRules };
            Finished?.Invoke();
        }

        public void Cancel()
        {
            Answer = new SetUpSiteAnswer { Apply = false };
            Finished?.Invoke();
        }

        private bool Run(Func<SetUpSiteView> call, double slope)
        {
            Model.ClearProblem();
            try
            {
                SetUpSiteView next = call();
                Model.Load(next);
                Model.Keep(slope);
                Model.MarkDry(slope);
                return true;
            }
            catch (Exception error)
            {
                Model.Fail(error.Message);
                return false;
            }
        }
    }
}
