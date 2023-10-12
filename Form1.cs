using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using NativeWifi;
using System.Net.NetworkInformation;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NetScanBeta
{
    public partial class Form1 : Form
    {
        private NetworkInterface connectedNetworkInterface; // Store the connected network interface

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Clear the ListBox before scanning and populating
            listBox1.Items.Clear();

            // Scan and populate available WiFi networks
            ScanAndPopulateWiFiNetworks();
        }

        private void ScanAndPopulateWiFiNetworks()
        {
            try
            {
                WlanClient client = new WlanClient();
                foreach (WlanClient.WlanInterface wlanInterface in client.Interfaces)
                {
                    Wlan.WlanAvailableNetwork[] networks = wlanInterface.GetAvailableNetworkList(0);

                    foreach (Wlan.WlanAvailableNetwork network in networks)
                    {
                        // Add the SSID to the ListBox
                        string ssid = Encoding.ASCII.GetString(network.dot11Ssid.SSID, 0, (int)network.dot11Ssid.SSIDLength);
                        listBox1.Items.Add(ssid);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error");
            }
        }

        private async void connectButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if manual SSID entry is enabled
                if (manualSSIDCheckBox.Checked)
                {
                    // Use the manually entered SSID and password
                    string manualSSID = ssidTextBox.Text;
                    string password = passwordTextBox.Text;

                    // Implement the connection logic using manualSSID and password
                    connectedNetworkInterface = ConnectToWiFiNetwork(manualSSID, password);
                }
                else
                {
                    // Get the selected SSID from the ListBox
                    string selectedSSID = listBox1.SelectedItem as string;

                    if (string.IsNullOrEmpty(selectedSSID))
                    {
                        MessageBox.Show("Please select an SSID from the list.", "Error");
                    }
                    else
                    {
                        // Implement the connection logic using selectedSSID and passwordTextBox.Text
                        connectedNetworkInterface = ConnectToWiFiNetwork(selectedSSID, passwordTextBox.Text);
                    }
                }

                // After connecting, retrieve and display the MAC address manufacturer
                if (connectedNetworkInterface != null)
                {
                    string macAddress = connectedNetworkInterface.GetPhysicalAddress().ToString();
                    string manufacturer = await GetMacAddressManufacturerAsync(macAddress);
                    ResultsLabel.Text = $"Connected to network. MAC Address: {macAddress}, Manufacturer: {manufacturer}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error");
            }
        }

        public static NetworkInterface ConnectToWiFiNetwork(string ssid, string password)
        {
            try
            {
                // Create the folder for WLAN profiles if it doesn't exist
                string profilesFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\WLANProfiles";
                if (!Directory.Exists(profilesFolder))
                {
                    Directory.CreateDirectory(profilesFolder);
                }

                // Use the netsh command to configure and connect to the WiFi network
                string profileName = $"WiFiProfile_{Guid.NewGuid()}";
                string xml = $@"<?xml version='1.0'?>
        <WLANProfile xmlns='http://www.microsoft.com/networking/WLAN/profile/v1'>
            <name>{profileName}</name>
            <SSIDConfig>
                <SSID>
                    <name>{ssid}</name>
                </SSID>
            </SSIDConfig>
            <connectionType>ESS</connectionType>
            <connectionMode>manual</connectionMode>
            <MSM>
                <security>
                    <authEncryption>
                        <authentication>WPA2PSK</authentication>
                        <encryption>AES</encryption>
                        <useOneX>false</useOneX>
                    </authEncryption>
                    <sharedKey>
                        <keyType>passPhrase</keyType>
                        <protected>false</protected>
                        <keyMaterial>{password}</keyMaterial>
                    </sharedKey>
                </security>
            </MSM>
        </WLANProfile>";

                // Save the profile
                string profilePath = Path.Combine(profilesFolder, $"{profileName}.xml");
                File.WriteAllText(profilePath, xml);

                // Add the profile
                var psi = new ProcessStartInfo("netsh", $"add profile filename=\"{profilePath}\"");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi).WaitForExit();

                // Connect to the network
                psi = new ProcessStartInfo("netsh", $"wlan connect name=\"{ssid}\"");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi).WaitForExit();

                // Remove the temporary profile
                psi = new ProcessStartInfo("netsh", $"delete profile name=\"{profileName}\"");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi).WaitForExit();

                // Get the connected network interface
                NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface networkInterface in networkInterfaces)
                {
                    if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        return networkInterface;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error");
            }

            return null;
        }

        public async Task<string> GetMacAddressManufacturerAsync(string macAddress)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Send a request to a MAC address manufacturer lookup service
                    string apiUrl = $"https://macvendors.co/api/{macAddress}";
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        if (content.Contains("result not found"))
                        {
                            return "Manufacturer not found";
                        }

                        // Parse the JSON response to get the manufacturer
                        dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(content);
                        return data["result"]["company"];
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error looking up MAC address manufacturer: " + ex.Message, "Error");
            }

            return "Unknown";
        }
    }
}
