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

        public SetUpSiteSession(SetUpSiteView view, SetUpSiteCallbacks callbacks, Func<string> pickSheet)
        {
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

        public void Apply()
        {
            if (!Model.CanApply)
            {
                return;
            }
            Answer = new SetUpSiteAnswer { Apply = true, RampSlope = Model.SelectedSlope, ParameterSheet = Model.ChosenSheet };
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
