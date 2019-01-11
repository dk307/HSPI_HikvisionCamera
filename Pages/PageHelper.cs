using HomeSeerAPI;
using NullGuard;
using Scheduler;
using System;
using System.Collections.Specialized;
using System.Text;
using System.Web;
using static System.FormattableString;

namespace Hspi.Pages
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class PageHelper : PageBuilderAndMenu.clsPageBuilder
    {
        public PageHelper(IHSApplication HS, PluginConfig pluginConfig, string pageName) : base(pageName)
        {
            this.HS = HS;
            this.pluginConfig = pluginConfig;
        }

        public static string HtmlEncode<T>([AllowNull]T value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return HttpUtility.HtmlEncode(value);
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

        protected static string HtmlTextBox(string name, [AllowNull]string defaultText, int size = 25, string type = "text", bool @readonly = false)
        {
            return Invariant($"<input type=\'{type}\' id=\'{NameToIdWithPrefix(name)}\' size=\'{size}\' name=\'{name}\' value=\'{HtmlEncode(defaultText)}\' {(@readonly ? "readonly" : string.Empty)}>");
        }

        protected static string NameToIdWithPrefix(string name)
        {
            return Invariant($"{ IdPrefix}{NameToId(name)}");
        }

        protected static string TextArea(string name, [AllowNull]string defaultText, int rows = 6, int cols = 120, bool @readonly = false)
        {
            return Invariant($"<textarea form_id=\'{NameToIdWithPrefix(name)}\' rows=\'{rows}\' cols=\'{cols}\' name=\'{name}\'  {(@readonly ? "readonly" : string.Empty)}>{HtmlEncode(defaultText)}</textarea>");
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

        protected string FormDropDown(string name, NameValueCollection options, string selected,
                                                                      int width, string tooltip, bool autoPostBack)
        {
            return FormDropDown(name, options, selected,
                              width, tooltip, autoPostBack, PageName);
        }

        protected string FormPageButton(string name, string label)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
            };

            return b.Build();
        }

        protected string FormTimeSpan(string name, string label, TimeSpan timeSpan, bool submit)
        {
            var b = new clsJQuery.jqTimeSpanPicker(name, label, PageName, submit)
            {
                id = NameToIdWithPrefix(name),
                defaultTimeSpan = timeSpan,
            };

            return b.Build();
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

        private static string NameToId(string name)
        {
            return name.Replace(' ', '_');
        }

        protected const string PageTypeId = "pagetype";
        protected const string RecordId = "recordid";
        protected readonly IHSApplication HS;
        protected readonly PluginConfig pluginConfig;
        private const string IdPrefix = "id_";
    }
}