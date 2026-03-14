using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace UUPDumpWPF
{
    public partial class VirtualEditionsDialog : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly string _currentVAutoEditions;

        public VirtualEditionsDialog(MainWindow owner, string currentVAutoEditions)
        {
            InitializeComponent();
            Owner = owner;
            _mainWindow = owner;
            _currentVAutoEditions = currentVAutoEditions;
            
            // Pre-check checkboxes based on current virtual editions
            PreCheckEditions();
        }

        private void PreCheckEditions()
        {
            // Always enable checkboxes when dialog opens for Pro edition
            EnableAllCheckboxes();
            
            if (string.IsNullOrEmpty(_currentVAutoEditions))
            {
                // No virtual editions currently selected - leave checkboxes unchecked but enabled
                return;
            }

            var selectedEditions = _currentVAutoEditions.Split(',').Select(s => s.Trim()).ToList();

            ChkProWorkstation.IsChecked = selectedEditions.Contains("ProfessionalWorkstation");
            ChkProEducation.IsChecked = selectedEditions.Contains("ProfessionalEducation");
            ChkEducation.IsChecked = selectedEditions.Contains("Education");
            ChkEnterprise.IsChecked = selectedEditions.Contains("Enterprise");
            ChkServerRdsh.IsChecked = selectedEditions.Contains("ServerRdsh");
            ChkIoTEnterprise.IsChecked = selectedEditions.Contains("IoTEnterprise");
            ChkIoTEnterpriseK.IsChecked = selectedEditions.Contains("IoTEnterpriseK");
        }

        private void EnableAllCheckboxes()
        {
            ChkProWorkstation.IsEnabled = true;
            ChkProEducation.IsEnabled = true;
            ChkEducation.IsEnabled = true;
            ChkEnterprise.IsEnabled = true;
            ChkServerRdsh.IsEnabled = true;
            ChkIoTEnterprise.IsEnabled = true;
            ChkIoTEnterpriseK.IsEnabled = true;
        }

        private void DisableAllCheckboxes()
        {
            ChkProWorkstation.IsEnabled = false;
            ChkProEducation.IsEnabled = false;
            ChkEducation.IsEnabled = false;
            ChkEnterprise.IsEnabled = false;
            ChkServerRdsh.IsEnabled = false;
            ChkIoTEnterprise.IsEnabled = false;
            ChkIoTEnterpriseK.IsEnabled = false;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (ChkProWorkstation.IsEnabled)
            {
                ChkProWorkstation.IsChecked = true;
                ChkProEducation.IsChecked = true;
                ChkEducation.IsChecked = true;
                ChkEnterprise.IsChecked = true;
                ChkServerRdsh.IsChecked = true;
                ChkIoTEnterprise.IsChecked = true;
                ChkIoTEnterpriseK.IsChecked = true;
            }
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            if (ChkProWorkstation.IsEnabled)
            {
                ChkProWorkstation.IsChecked = false;
                ChkProEducation.IsChecked = false;
                ChkEducation.IsChecked = false;
                ChkEnterprise.IsChecked = false;
                ChkServerRdsh.IsChecked = false;
                ChkIoTEnterprise.IsChecked = false;
                ChkIoTEnterpriseK.IsChecked = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // If checkboxes are disabled, don't save anything
            if (!ChkProWorkstation.IsEnabled)
            {
                DialogResult = false;
                return;
            }

            var selectedEditions = new List<string>();

            if (ChkProWorkstation.IsChecked == true) selectedEditions.Add("ProfessionalWorkstation");
            if (ChkProEducation.IsChecked == true) selectedEditions.Add("ProfessionalEducation");
            if (ChkEducation.IsChecked == true) selectedEditions.Add("Education");
            if (ChkEnterprise.IsChecked == true) selectedEditions.Add("Enterprise");
            if (ChkServerRdsh.IsChecked == true) selectedEditions.Add("ServerRdsh");
            if (ChkIoTEnterprise.IsChecked == true) selectedEditions.Add("IoTEnterprise");
            if (ChkIoTEnterpriseK.IsChecked == true) selectedEditions.Add("IoTEnterpriseK");

            if (selectedEditions.Count > 0)
            {
                // Virtual editions are now handled automatically by the script
                _mainWindow.ChkStartVirtualPublic.IsChecked = true;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}
