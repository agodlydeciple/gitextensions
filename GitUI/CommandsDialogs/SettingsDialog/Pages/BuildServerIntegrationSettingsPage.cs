using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GitCommands;
using GitCommands.Settings;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitUI.CommandsDialogs.SettingsDialog.Pages
{
    public partial class BuildServerIntegrationSettingsPage : RepoDistSettingsPage
    {
        private const string NoneItem = "<None>";
        private Task<object> _populateBuildServerTypeTask;

        public BuildServerIntegrationSettingsPage()
        {
            InitializeComponent();
            Text = "Build server integration";
            Translate();
        }

        protected override void Init(ISettingsPageHost aPageHost)
        {
            base.Init(aPageHost);

            _populateBuildServerTypeTask =
                Task.Factory.StartNew(() =>
                        {
                            var exports = ManagedExtensibility.CompositionContainer.GetExports<IBuildServerAdapter, IBuildServerTypeMetadata>();
                            var buildServerTypes = exports.Select(export => export.Metadata.BuildServerType).ToArray();

                            return buildServerTypes;
                        })
                    .ContinueWith(
                        task =>
                            {
                                checkBoxEnableBuildServerIntegration.Enabled = true;
                                BuildServerType.Enabled = true;

                                BuildServerType.DataSource = new[] { NoneItem }.Concat(task.Result).ToArray();
                                return BuildServerType.DataSource;
                            },
                        TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override bool IsInstantSavePage
        {
            get { return true; }
        }

        protected override void SettingsToPage()
        {
            _populateBuildServerTypeTask.ContinueWith(
                task =>
                {
                    checkBoxEnableBuildServerIntegration.Checked = CurrentSettings.BuildServer.EnableIntegration;

                    BuildServerType.SelectedItem = CurrentSettings.BuildServer.Type.Value ?? NoneItem;
                },
                TaskScheduler.FromCurrentSynchronizationContext());
        }
        private bool IsRepositoryValid
        {
            get { return !string.IsNullOrEmpty(Module.GitWorkingDir); }
        }        

        protected override void PageToSettings()
        {
            CurrentSettings.BuildServer.EnableIntegration = checkBoxEnableBuildServerIntegration.Checked;

            var selectedBuildServerType = GetSelectedBuildServerType();

            CurrentSettings.BuildServer.Type.Value = NoneItem.Equals(selectedBuildServerType) ? null : selectedBuildServerType;

            var control =
                buildServerSettingsPanel.Controls.OfType<IBuildServerSettingsUserControl>()
                                        .SingleOrDefault();
            if (control != null)
                control.SaveSettings(CurrentSettings.BuildServer.TypeSettings);
        }

        private void ActivateBuildServerSettingsControl()
        {
            var controls = buildServerSettingsPanel.Controls.OfType<IBuildServerSettingsUserControl>().Cast<Control>();
            var previousControl = controls.SingleOrDefault();
            if (previousControl != null) previousControl.Dispose();

            var control = CreateBuildServerSettingsUserControl();

            buildServerSettingsPanel.Controls.Clear();

            if (control != null)
            {
                control.LoadSettings(CurrentSettings.BuildServer.TypeSettings);

                buildServerSettingsPanel.Controls.Add((Control)control);
            }
        }

        private IBuildServerSettingsUserControl CreateBuildServerSettingsUserControl()
        {
            if (!Equals(BuildServerType.SelectedItem, NoneItem) && !string.IsNullOrEmpty(Module.GitWorkingDir))
            {
                var defaultProjectName = Module.GitWorkingDir.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Last();

                var exports = ManagedExtensibility.CompositionContainer.GetExports<IBuildServerSettingsUserControl, IBuildServerTypeMetadata>();
                var selectedExport = exports.SingleOrDefault(export => export.Metadata.BuildServerType == GetSelectedBuildServerType());
                if (selectedExport != null)
                {
                    var buildServerSettingsUserControl = selectedExport.Value;
                    buildServerSettingsUserControl.Initialize(defaultProjectName);
                    return buildServerSettingsUserControl;
                }
            }

            return null;
        }

        private string GetSelectedBuildServerType()
        {
            return (string)BuildServerType.SelectedItem;
        }

        private void BuildServerType_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            ActivateBuildServerSettingsControl();
        }
    }
}
