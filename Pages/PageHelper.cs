using Scheduler;
using NullGuard;
using static System.FormattableString;
using HomeSeerAPI;
using System.Web;
using System.Collections.Specialized;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Hspi.Pages
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class PageHelper : PageBuilderAndMenu.clsPageBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigPage" /> class.
        /// </summary>
        /// <param name="HS">The hs.</param>
        /// <param name="pluginConfig">The plugin configuration.</param>
        public PageHelper(IHSApplication HS, PluginConfig pluginConfig) : base(pageName)
        {
            this.HS = HS;
            this.pluginConfig = pluginConfig;
        }

        protected enum PageType
        {
            Default,
            AddCamera,
            EditCamera,
            AddCameraProperty,
            EditCameraProperty,
        };

        /// <summary>
        /// Gets the name of the web page.
        /// </summary>
        public static string Name => pageName;

        /// <summary>
        /// Get the web page string for the configuration page.
        /// </summary>
        /// <returns>
        /// System.String.
        /// </returns>
        public string GetWebPage(string queryString)
        {
            try
            {
                reset();
                this.UsesJqAll = false;

                NameValueCollection parts = HttpUtility.ParseQueryString(queryString);

                if (Enum.TryParse<PageType>(parts[PageTypeId], true, out var pageType))
                {
                    return GetWebPage(pageType, parts);
                }
                else
                {
                    return GetWebPage(PageType.Default, parts);
                }
            }
            catch (Exception)
            {
                return "error";
            }
        }

        protected static string HtmlTextBox(string name, [AllowNull]string defaultText, int size = 25, string type = "text", bool @readonly = false)
        {
            return Invariant($"<input type=\'{type}\' id=\'{NameToIdWithPrefix(name)}\' size=\'{size}\' name=\'{name}\' value=\'{HtmlEncode(defaultText)}\' {(@readonly ? "readonly" : string.Empty)}>");
        }

        protected static string NameToIdWithPrefix(string name)
        {
            return Invariant($"{ IdPrefix}{NameToId(name)}");
        }

        protected string FormButton(string name, string label, string toolTip)
        {
            var button = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
                toolTip = toolTip,
            };
            button.toolTip = toolTip;
            button.enabled = true;

            return button.Build();
        }

        protected string FormCheckBox(string name, string label, bool @checked, bool autoPostBack = false)
        {
            this.UsesjqCheckBox = true;
            var cb = new clsJQuery.jqCheckBox(name, label, PageName, true, true)
            {
                id = NameToIdWithPrefix(name),
                @checked = @checked,
                autoPostBack = autoPostBack,
            };
            return cb.Build();
        }

        protected string FormPageButton(string name, string label)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
            };

            return b.Build();
        }

        protected static string FormDropDown(string name, NameValueCollection options, string selected,
                              int width, string tooltip, bool autoPostBack, string pageName)
        {
            var dropdown = new clsJQuery.jqDropList(name, pageName, false)
            {
                selectedItemIndex = -1,
                id = NameToIdWithPrefix(name),
                autoPostBack = autoPostBack,
                toolTip = tooltip,
                style = Invariant($"width: {width}px;"),
                enabled = true,
                submitForm = autoPostBack,
            };

            if (options != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    var sel = options.GetKey(i) == selected;
                    dropdown.AddItem(options.Get(i), options.GetKey(i), sel);
                }
            }

            return dropdown.Build();
        }

        protected string PageTypeButton(string name, string label, PageType type, string id = null)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, false)
            {
                id = NameToIdWithPrefix(name),
                url = Invariant($"/{pageUrl}?{PageTypeId}={HttpUtility.UrlEncode(type.ToString())}&{RecordId}={HttpUtility.UrlEncode(id ?? string.Empty)}"),
            };

            return b.Build();
        }

        protected abstract string GetWebPage(PageType pageType, NameValueCollection parts);

        private static string NameToId(string name)
        {
            return name.Replace(' ', '_');
        }

        protected void IncludeResourceCSS(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<style type=\"text/css\">");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</style>");
            this.AddScript(stb.ToString());
        }

        protected void IncludeResourceScript(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</script>");
            this.AddScript(stb.ToString());
        }

        public static string HtmlEncode<T>([AllowNull]T value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return HttpUtility.HtmlEncode(value);
        }

        protected static readonly string pageName = Invariant($"{PluginData.PlugInName} Configuration").Replace(' ', '_');
        protected static readonly string pageUrl = HttpUtility.UrlEncode(pageName);
        protected readonly IHSApplication HS;
        protected readonly PluginConfig pluginConfig;
        private const string IdPrefix = "id_";
        private const string PageTypeId = "pagetype";
        protected const string RecordId = "recordid";
    }
}