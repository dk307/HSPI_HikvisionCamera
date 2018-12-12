using HomeSeerAPI;
using Hspi.Camera;
using NullGuard;
using Scheduler;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using static System.FormattableString;

namespace Hspi.Pages
{
    /// <summary>
    /// Helper class to generate configuration page for plugin
    /// </summary>
    /// <seealso cref="Scheduler.PageBuilderAndMenu.clsPageBuilder" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal partial class ConfigPage : PageHelper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigPage" /> class.
        /// </summary>
        /// <param name="HS">The hs.</param>
        /// <param name="pluginConfig">The plugin configuration.</param>
        public ConfigPage(IHSApplication HS, PluginConfig pluginConfig) : base(HS, pluginConfig)
        {
        }

        protected override string GetWebPage(PageType pageType, NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();

            switch (pageType)
            {
                case PageType.EditCamera:
                case PageType.AddCamera:
                    {
                        stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                        stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));
                        pluginConfig.Cameras.TryGetValue(parts[RecordId], out var camera);
                        stb.Append(BuildAddNewCameraWebPageBody(camera));
                        stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                        AddBody(stb.ToString());
                        AddFooter(HS.GetPageFooter());
                        break;
                    }

                case PageType.EditCameraProperty:
                case PageType.AddCameraProperty:
                    {
                        stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                        stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));
                        pluginConfig.CameraProperties.TryGetValue(parts[RecordId], out var cameraProperty);
                        stb.Append(BuildAddNewCameraPropertyWebPageBody(cameraProperty));
                        stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                        AddBody(stb.ToString());
                        AddFooter(HS.GetPageFooter());
                        break;
                    }

                default:
                case PageType.Default:
                    {
                        stb.Append(HS.GetPageHeader(Name, "Configuration", string.Empty, string.Empty, false, false));
                        stb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("pluginpage", string.Empty));
                        stb.Append(BuildDefaultWebPageBody(parts));
                        stb.Append(PageBuilderAndMenu.clsPageBuilder.DivEnd());
                        AddBody(stb.ToString());
                        AddFooter(HS.GetPageFooter());
                        break;
                    }
            }

            suppressDefaultFooter = true;
            return BuildPage();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private string BuildAddNewCameraWebPageBody([AllowNull]CameraSettings cameraSettings)
        {
            TimeSpan DefaultAlarmCancelInterval = TimeSpan.FromSeconds(30);
            TimeSpan DefaultCameraPropertiesRefreshInterval = TimeSpan.FromSeconds(60);

            string id = cameraSettings?.Id ?? Guid.NewGuid().ToString();
            string name = cameraSettings?.Name ?? string.Empty;
            string hostName = cameraSettings?.CameraHost ?? @"http://";
            string userId = cameraSettings?.Login ?? string.Empty;
            string password = cameraSettings?.Password ?? string.Empty;
            string alarmCancelInterval = (cameraSettings?.AlarmCancelInterval ?? DefaultAlarmCancelInterval).TotalSeconds.ToString(CultureInfo.InvariantCulture);
            string propertiesRefreshInterval = (cameraSettings?.CameraPropertiesRefreshInterval ?? DefaultCameraPropertiesRefreshInterval).TotalSeconds.ToString(CultureInfo.InvariantCulture);
            string snapshotDownloadDirectory = cameraSettings?.SnapshotDownloadDirectory ?? string.Empty;
            string videoDownloadDirectory = cameraSettings?.VideoDownloadDirectory ?? string.Empty;

            string buttonLabel = cameraSettings != null ? "Save" : "Add";
            string header = cameraSettings != null ? "Edit Camera" : "Add New Camera";

            StringBuilder stb = new StringBuilder();

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmCameraChange", "IdChange", "Post"));

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td></td><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader' colspan=2>{header}</td></tr>"));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.Name), name));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Camera:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.CameraHost), hostName, size: 75));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>User:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.Login), userId));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Password:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.Password), password, type: "password"));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Alarm Cancel Interval(seconds):</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.AlarmCancelInterval), alarmCancelInterval));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Properties Refresh Interval(seconds):</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.CameraPropertiesRefreshInterval), propertiesRefreshInterval));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Snapshot download directory:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.SnapshotDownloadDirectory), snapshotDownloadDirectory));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Video download directory:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraSettings.VideoDownloadDirectory), videoDownloadDirectory));
            stb.Append("</td></tr>");

            stb.Append(Invariant($"<tr><td colspan=2>{HtmlTextBox(RecordId, id, type: "hidden")}<div id='{SaveErrorDivId}' style='color:Red'></div></td><td></td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{FormPageButton(SaveCamera, buttonLabel)}"));

            if (cameraSettings != null)
            {
                stb.Append(FormPageButton(DeleteCamera, "Delete"));
            }

            stb.Append(FormPageButton(CancelCamera, "Cancel"));
            stb.Append(Invariant($"</td></tr>"));
            stb.Append("<tr height='5'><td colspan=2></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private string BuildAddNewCameraPropertyWebPageBody([AllowNull]CameraProperty cameraProperties)
        {
            string id = cameraProperties?.Id ?? Guid.NewGuid().ToString();
            string name = cameraProperties?.Name ?? string.Empty;
            string urlPath = cameraProperties?.UrlPath ?? string.Empty;
            string xpath = cameraProperties?.XPathForGet.Path.Expression ?? string.Empty;
            string type = cameraProperties?.CameraPropertyType.ToString() ?? CameraProperty.Type.String.ToString();

            string buttonLabel = cameraProperties != null ? "Save" : "Add";
            string header = cameraProperties != null ? "Edit Camera Property" : "Add New Camera Property";

            NameValueCollection collection = new NameValueCollection();
            collection.Add(CameraProperty.Type.String.ToString(), CameraProperty.Type.String.ToString());
            collection.Add(CameraProperty.Type.Number.ToString(), CameraProperty.Type.Number.ToString());

            StringBuilder stb = new StringBuilder();

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmCameraPropertyChange", "IdChange", "Post"));

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td></td><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader' colspan=2>{header}</td></tr>"));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraProperty.Name), name));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>UrlPath:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraProperty.UrlPath), urlPath, size: 90));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>XPath:</td><td class='tablecell'>"));
            stb.Append(HtmlTextBox(nameof(CameraProperty.XPathForGet), xpath, size: 90));
            stb.Append("</td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Type:</td><td class='tablecell'>"));
            if (cameraProperties == null)
            {
                stb.Append(FormDropDown(nameof(CameraProperty.Type), collection, type, 100, string.Empty, false, string.Empty));
            }
            else
            {
                stb.Append(HtmlTextBox(nameof(CameraProperty.Type), type, type: "hidden"));
            }
            stb.Append("</td></tr>");

            stb.Append(Invariant($"<tr><td colspan=2>{HtmlTextBox(RecordId, id, type: "hidden")}<div id='{SaveErrorDivId}' style='color:Red'></div></td><td></td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{FormPageButton(SaveCameraProperty, buttonLabel)}"));

            if (cameraProperties != null)
            {
                stb.Append(FormPageButton(DeleteCameraProperty, "Delete"));
            }

            stb.Append(FormPageButton(CancelCameraProperty, "Cancel"));
            stb.Append(Invariant($"</td></tr>"));
            stb.Append("<tr height='5'><td colspan=2></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        /// <summary>
        /// The user has selected a control on the configuration web page.
        /// The post data is provided to determine the control that initiated the post and the state of the other controls.
        /// </summary>
        /// <param name="data">The post data.</param>
        /// <param name="user">The name of logged in user.</param>
        /// <param name="userRights">The rights of the logged in user.</param>
        /// <returns>Any serialized data that needs to be passed back to the web page, generated by the clsPageBuilder class.</returns>
        public string PostBackProc(string data, [AllowNull]string user, int userRights)
        {
            NameValueCollection parts = HttpUtility.ParseQueryString(data);

            string form = parts["id"];

            if (form == NameToIdWithPrefix(SettingSaveButtonName))
            {
                HandleSaveMainSettingPostBack(parts);
            }
            else if ((form == NameToIdWithPrefix(DeleteCamera)) ||
                     (form == NameToIdWithPrefix(CancelCamera)) ||
                     (form == NameToIdWithPrefix(SaveCamera)))
            {
                HandleCameraPostBack(parts, form);
            }
            else if ((form == NameToIdWithPrefix(DeleteCameraProperty)) ||
                     (form == NameToIdWithPrefix(CancelCameraProperty)) ||
                     (form == NameToIdWithPrefix(SaveCameraProperty)))
            {
                HandleCameraPropertyPostBack(parts, form);
            }

            return base.postBackProc(Name, data, user, userRights);
        }

        private void HandleCameraPostBack(NameValueCollection parts, string form)
        {
            if (form == NameToIdWithPrefix(DeleteCamera))
            {
                this.pluginConfig.RemoveCamera(parts[RecordId]);
                this.pluginConfig.FireConfigChanged();
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
            }
            else if (form == NameToIdWithPrefix(CancelCamera))
            {
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
            }
            else if (form == NameToIdWithPrefix(SaveCamera))
            {
                StringBuilder results = new StringBuilder();
                string cameraName = parts[nameof(CameraSettings.Name)];
                if (string.IsNullOrWhiteSpace(cameraName))
                {
                    results.AppendLine("Camera Name is empty.<br>");
                }

                string userId = parts[nameof(CameraSettings.Login)];
                string password = parts[nameof(CameraSettings.Password)];

                string cameraHostString = parts[nameof(CameraSettings.CameraHost)];
                if (!Uri.TryCreate(cameraHostString, UriKind.Absolute, out var cameraHost))
                {
                    results.AppendLine("Camera address is not valid.<br>");
                }

                var alarmCancelIntervalString = parts[nameof(CameraSettings.AlarmCancelInterval)];
                if (!long.TryParse(alarmCancelIntervalString, NumberStyles.Any, CultureInfo.InvariantCulture, out var alarmCancelInterval) ||
                    alarmCancelInterval < 0)
                {
                    results.AppendLine("Alarm cancel interval is not valid.<br>");
                }

                var propertiesRefreshIntervalString = parts[nameof(CameraSettings.CameraPropertiesRefreshInterval)];
                if (!long.TryParse(propertiesRefreshIntervalString, NumberStyles.Any, CultureInfo.InvariantCulture, out var propertiesRefreshInterval) ||
                    alarmCancelInterval < 0)
                {
                    results.AppendLine("Properties refresh interval is not valid.<br>");
                }

                var snapshotDirectory = parts[nameof(CameraSettings.SnapshotDownloadDirectory)];
                if (!Directory.Exists(snapshotDirectory))
                {
                    results.AppendLine("Snapshot directory is not valid.<br>");
                }

                var videoDownloadDirectory = parts[nameof(CameraSettings.VideoDownloadDirectory)];
                if (!Directory.Exists(videoDownloadDirectory))
                {
                    results.AppendLine("Video download directory is not valid.<br>");
                }

                if (results.Length > 0)
                {
                    this.divToUpdate.Add(SaveErrorDivId, results.ToString());
                }
                else
                {
                    string id = parts[RecordId];

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        id = System.Guid.NewGuid().ToString();
                    }

                    var data = new CameraSettings(id,
                                                  cameraName,
                                                  cameraHost.AbsoluteUri.ToString(),
                                                  userId,
                                                  password,
                                                  TimeSpan.FromSeconds(alarmCancelInterval),
                                                  pluginConfig.CameraProperties,
                                                  TimeSpan.FromSeconds(propertiesRefreshInterval),
                                                  snapshotDirectory,
                                                  videoDownloadDirectory);

                    this.pluginConfig.AddCamera(data);
                    this.pluginConfig.FireConfigChanged();
                    this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=1")));
                }
            }
        }

        private void HandleCameraPropertyPostBack(NameValueCollection parts, string form)
        {
            if (form == NameToIdWithPrefix(DeleteCameraProperty))
            {
                this.pluginConfig.RemoveCameraProperty(parts[RecordId]);
                this.pluginConfig.FireConfigChanged();
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=2")));
            }
            else if (form == NameToIdWithPrefix(CancelCameraProperty))
            {
                this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=2")));
            }
            else if (form == NameToIdWithPrefix(SaveCameraProperty))
            {
                StringBuilder results = new StringBuilder();
                string cameraPropertyName = parts[nameof(CameraProperty.Name)];
                if (string.IsNullOrWhiteSpace(cameraPropertyName))
                {
                    results.AppendLine("Name is empty.<br>");
                }

                string cameraPropertyUrlPath = parts[nameof(CameraProperty.UrlPath)];
                if (string.IsNullOrWhiteSpace(cameraPropertyUrlPath))
                {
                    results.AppendLine("UrlPath is empty.<br>");
                }

                string cameraPropertyXPath = parts[nameof(CameraProperty.XPathForGet)];
                if (string.IsNullOrWhiteSpace(cameraPropertyXPath))
                {
                    results.AppendLine("Xpath is empty.<br>");
                }

                try
                {
                    XmlPathData xPath = new XmlPathData(cameraPropertyXPath);
                    var path = xPath.Path;
                }
                catch (Exception)
                {
                    results.AppendLine("XPath is not valid.<br>");
                }

                if (!Enum.TryParse<CameraProperty.Type>(parts[nameof(CameraProperty.Type)], out var type))
                {
                    results.AppendLine("Type is not valid.<br>");
                }

                if (results.Length > 0)
                {
                    this.divToUpdate.Add(SaveErrorDivId, results.ToString());
                }
                else
                {
                    string id = parts[RecordId];

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        id = System.Guid.NewGuid().ToString();
                    }

                    var data = new CameraProperty(id,
                                                  cameraPropertyName,
                                                  cameraPropertyUrlPath,
                                                  cameraPropertyXPath,
                                                  type);

                    this.pluginConfig.AddCameraProperty(data);
                    this.pluginConfig.FireConfigChanged();
                    this.divToUpdate.Add(SaveErrorDivId, RedirectPage(Invariant($"/{pageUrl}?{TabId}=2")));
                }
            }
        }

        private string BuildMainSettingTab()
        {
            StringBuilder stb = new StringBuilder();
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmSettings", "IdSettings", "Post"));

            stb.Append(@"<br>");
            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr height='5'><td style='width:35%'></td><td style='width:65%'></td></tr>");
            stb.Append(Invariant($"<tr><td class='tablecell'>Debug Logging Enabled:</td><td class='tablecell'>{FormCheckBox(DebugLoggingId, string.Empty, this.pluginConfig.DebugLogging)}</td ></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2><div id='{ErrorDivId}' style='color:Red'></div></td></tr>"));
            stb.Append(Invariant($"<tr><td colspan=2>{FormButton(SettingSaveButtonName, "Save", "Save Settings")}</td></tr>"));
            stb.Append("<tr height='5'><td colspan=2></td></tr>");
            stb.Append(@" </table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        /// <summary>
        /// Builds the web page body for the configuration page.
        /// The page has separate forms so that only the data in the appropriate form is returned when a button is pressed.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.Int32.TryParse(System.String,System.Int32@)")]
        private string BuildDefaultWebPageBody(NameValueCollection parts)
        {
            this.UsesJqTabs = true;
            string tab = parts[TabId] ?? "0";
            int defaultTab = 0;
            int.TryParse(tab, out defaultTab);

            int i = 0;
            StringBuilder stb = new StringBuilder();

            var tabs = new clsJQuery.jqTabs("tab1id", PageName);
            var tab1 = new clsJQuery.Tab();
            tab1.tabTitle = "Settings";
            tab1.tabDIVID = Invariant($"tabs{i++}");
            tab1.tabContent = BuildMainSettingTab();
            tabs.tabs.Add(tab1);

            var tab2 = new clsJQuery.Tab();
            tab2.tabTitle = "Cameras";
            tab2.tabDIVID = Invariant($"tabs{i++}");
            tab2.tabContent = BuildCamerasTab(parts);
            tabs.tabs.Add(tab2);

            var tab3 = new clsJQuery.Tab();
            tab3.tabTitle = "Camera Properties";
            tab3.tabDIVID = Invariant($"tabs{i++}");
            tab3.tabContent = BuildCamerasPropertiesTab(parts);
            tabs.tabs.Add(tab3);

            switch (defaultTab)
            {
                case 0:
                    tabs.defaultTab = tab1.tabDIVID;
                    break;

                case 1:
                    tabs.defaultTab = tab2.tabDIVID;
                    break;

                case 2:
                    tabs.defaultTab = tab3.tabDIVID;
                    break;
            }

            tabs.postOnTabClick = false;
            stb.Append(tabs.Build());

            return stb.ToString();
        }

        private string BuildCamerasTab(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();

            IncludeResourceCSS(stb, "jquery.dataTables.css");
            IncludeResourceScript(stb, "jquery.dataTables.min.js");

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr><td>");

            stb.Append("<table id=\"cameraTable\" class=\"cell-border compact\" style=\"width:100%\">");
            stb.Append(@"<thead><tr>");

            stb.Append(Invariant($"<th>Name</th>"));
            stb.Append(Invariant($"<th>Uri</th>"));
            stb.Append(Invariant($"<th></th>"));

            stb.Append(@"</tr></thead>");
            stb.Append(@"<tbody>");

            foreach (var pair in pluginConfig.Cameras)
            {
                var id = pair.Key;
                var device = pair.Value;

                stb.Append(@"<tr>");
                stb.Append(Invariant($"<td class='tablecell'>{HtmlEncode(device.Name)}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{HtmlEncode(device.CameraHost)}</td>"));
                stb.Append("<td class='tablecell'>");
                stb.Append(PageTypeButton(Invariant($"Edit{id}"), "Edit", PageType.EditCamera, id: id));
                stb.Append("</td></tr>");
            }
            stb.Append(@"</tbody>");
            stb.Append(@"</table>");

            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(@"$(document).ready(function() {");
            stb.AppendLine(@"$('#cameraTable').DataTable({
                                       'pageLength':10,
                                        'order': [],
                                        'columnDefs': [
                                            { 'className': 'dt-left', 'targets': '_all'}
                                        ],
                                        'columns': [
                                            null,
                                            null,
                                            { 'orderable': false }
                                          ]
                                    });
                                });");
            stb.AppendLine("</script>");

            stb.Append(Invariant($"<tr><td>{PageTypeButton("Add New Camera", "Add New Camera", PageType.AddCamera)}</td><td></td></tr>"));

            stb.Append(Invariant($"<tr><td></td></tr>"));
            stb.Append(@"<tr height='5'><td></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");

            return stb.ToString();
        }

        private string BuildCamerasPropertiesTab(NameValueCollection parts)
        {
            StringBuilder stb = new StringBuilder();

            IncludeResourceCSS(stb, "jquery.dataTables.css");
            IncludeResourceScript(stb, "jquery.dataTables.min.js");

            stb.Append(@"<div>");
            stb.Append(@"<table class='full_width_table'>");
            stb.Append("<tr><td>");

            stb.Append("<table id=\"cameraPropertyTable\" class=\"cell-border compact\" style=\"width:100%\">");
            stb.Append(@"<thead><tr>");

            stb.Append(Invariant($"<th>Name</th>"));
            stb.Append(Invariant($"<th>Type</th>"));
            stb.Append(Invariant($"<th>UrlPath</th>"));
            stb.Append(Invariant($"<th>XPath</th>"));
            stb.Append(Invariant($"<th></th>"));

            stb.Append(@"</tr></thead>");
            stb.Append(@"<tbody>");

            foreach (var pair in pluginConfig.CameraProperties)
            {
                var id = pair.Key;
                var cameraProperty = pair.Value;

                stb.Append(@"<tr>");
                stb.Append(Invariant($"<td class='tablecell'>{HtmlEncode(cameraProperty.Name)}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{HtmlEncode(cameraProperty.CameraPropertyType)}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{HtmlEncode(cameraProperty.UrlPath)}</td>"));
                stb.Append(Invariant($"<td class='tablecell'>{HtmlEncode(cameraProperty.XPathForGet.Path.Expression)}</td>"));
                stb.Append("<td class='tablecell'>");
                stb.Append(PageTypeButton(Invariant($"Edit{id}"), "Edit", PageType.EditCameraProperty, id: id));
                stb.Append("</td></tr>");
            }
            stb.Append(@"</tbody>");
            stb.Append(@"</table>");

            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(@"$(document).ready(function() {");
            stb.AppendLine(@"$('#cameraPropertyTable').DataTable({
                                       'pageLength':10,
                                        'order': [],
                                        'columnDefs': [
                                            { 'className': 'dt-left', 'targets': '_all'}
                                        ],
                                        'columns': [
                                            null,
                                            null,
                                            null,
                                            null,
                                            { 'orderable': false }
                                          ]
                                    });
                                });");
            stb.AppendLine("</script>");

            stb.Append(Invariant($"<tr><td>{PageTypeButton("Add New Camera Property", "Add New Camera Property", PageType.AddCameraProperty)}</td><td></td></tr>"));

            stb.Append(Invariant($"<tr><td></td></tr>"));
            stb.Append(@"<tr height='5'><td></td></tr>");
            stb.Append(@"</table>");
            stb.Append(@"</div>");

            return stb.ToString();
        }

        private void HandleSaveMainSettingPostBack(NameValueCollection parts)
        {
            StringBuilder results = new StringBuilder();

            // Validate

            if (results.Length > 0)
            {
                this.divToUpdate.Add(ErrorDivId, results.ToString());
            }
            else
            {
                this.divToUpdate.Add(ErrorDivId, string.Empty);

                this.pluginConfig.DebugLogging = parts[DebugLoggingId] == "checked";
                this.pluginConfig.FireConfigChanged();
            }
        }

        private const string DebugLoggingId = "DebugLoggingId";
        private const string ErrorDivId = "message_id";
        private const string IdPrefix = "id_";
        private const string SaveErrorDivId = "SaveErrorDivId";
        private const string SettingSaveButtonName = "SettingSave";
        private const string TabId = "tab";
        private const string CancelCamera = "CancelCamera";
        private const string DeleteCamera = "DeleteCamera";
        private const string SaveCamera = "SaveCamera";
        private const string CancelCameraProperty = "CancelCameraProperty";
        private const string DeleteCameraProperty = "DeleteCameraProperty";
        private const string SaveCameraProperty = "SaveCameraProperty";
    }
}