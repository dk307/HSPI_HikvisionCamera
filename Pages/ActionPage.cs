using HomeSeerAPI;
using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;

namespace Hspi.Pages
{
    internal class ActionPage : PageHelper
    {
        public ActionPage(IHSApplication HS, PluginConfig pluginConfig) : base(HS, pluginConfig, "Events")
        {
        }

        public IPlugInAPI.strMultiReturn GetRefreshActionPostUI([AllowNull] NameValueCollection postData, IPlugInAPI.strTrigActInfo actionInfo)
        {
            IPlugInAPI.strMultiReturn result = default;
            result.DataOut = actionInfo.DataIn;
            result.TrigActInfo = actionInfo;
            result.sResult = string.Empty;
            if (postData != null && postData.Count > 0)
            {
                var action = (actionInfo.DataIn != null) ?
                                                    (TakeSnapshotAction)ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) :
                                                    new TakeSnapshotAction();

                foreach (var pair in postData)
                {
                    string text = Convert.ToString(pair);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (text.StartsWith(nameof(TakeSnapshotAction.Id)))
                        {
                            action.Id = postData[text];
                        }
                        else if (text.StartsWith(NameToIdWithPrefix(nameof(TakeSnapshotAction.TimeSpan))))
                        {
                            try
                            {
                                action.TimeSpan = TimeSpan.Parse(postData[text], CultureInfo.InvariantCulture);
                            }
                            catch (Exception)
                            {
                                result.sResult += "<BR>Time span is not valid";
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(action.Id))
                {
                    result.sResult += "<BR>Camera is not valid";
                }
                result.DataOut = ObjectSerialize.SerializeToBytes(action);
            }

            return result;
        }

        public string GetRefreshActionUI(string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo)
        {
            StringBuilder stb = new StringBuilder();
            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as TakeSnapshotAction;

            var cameras = new NameValueCollection();
            foreach (var camera in pluginConfig.Cameras)
            {
                cameras.Add(camera.Key, camera.Value.Name);
            }

            stb.Append(FormDropDown(nameof(TakeSnapshotAction.Id) + uniqueControlId, cameras, action?.Id ?? string.Empty, 250, string.Empty, true));
            stb.Append("for &nbsp;");
            stb.Append(FormTimeSpan(nameof(TakeSnapshotAction.TimeSpan) + uniqueControlId, string.Empty, action?.TimeSpan ?? TimeSpan.Zero, true));
            return stb.ToString();
        }
    }
}