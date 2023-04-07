#if FORCE_DEBUG_MENU || UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeMarines.DebugMenus
{
    public class AutoPlaySubMenu : DebugSubMenu
    {
        public AutoPlaySubMenu(string name, KeyCode? shortcut = null, bool isMutiColumn = false, Func<string> getLabel = null, GUILayoutOption layout = null, Color? color = null, Func<bool> enabledCallback = null) 
            : base(name, shortcut, isMutiColumn, getLabel, layout, color, enabledCallback)
        {
        }

        protected override void OnAction()
        {
            base.OnAction();

            if (_elements.Count > 0)
            {
                return;
            }
            
            Add(new BackItem("Back"));

            Add(new DebugMenuItem("", () =>
            {
                List<AutoReloadMode> list = new List<AutoReloadMode>();

                foreach (AutoReloadMode type in Enum.GetValues(typeof(AutoReloadMode)))
                {
                    list.Add(type);
                }

                int index = list.IndexOf(LocalConfig.AutoReloadMode);
                if (Event.current.button == 2)
                {
                    index = 0;
                }
                else if (Event.current.button == 0)
                {
                    index = (index + 1) % list.Count;
                }
                else if (Event.current.button == 1)
                {
                    index = (index - 1) < 0 ? (list.Count - 1) : (index - 1);
                }

                LocalConfig.AutoReloadMode = list[index];
            },
            () => "Auto reload mode: " + LocalConfig.AutoReloadMode));

            Add(new DebugMenuItem("", () =>
            {
                List<AutoSpawnMode> list = new List<AutoSpawnMode>();

                foreach (AutoSpawnMode type in Enum.GetValues(typeof(AutoSpawnMode)))
                {
                    list.Add(type);
                }

                int index = list.IndexOf(LocalConfig.AutoSpawnMode);
                if (Event.current.button == 2)
                {
                    index = 0;
                }
                else if (Event.current.button == 0)
                {
                    index = (index + 1) % list.Count;
                }
                else if (Event.current.button == 1)
                {
                    index = (index - 1) < 0 ? (list.Count - 1) : (index - 1);
                }

                LocalConfig.AutoSpawnMode = list[index];
            },
            () => "Auto spawn mode: " + LocalConfig.AutoSpawnMode));

            Add(new DebugMenuItem("", () =>
            {
                List<AutoMergeMode> list = new List<AutoMergeMode>();

                foreach (AutoMergeMode type in Enum.GetValues(typeof(AutoMergeMode)))
                {
                    list.Add(type);
                }

                int index = list.IndexOf(LocalConfig.AutoMergeMode);
                if (Event.current.button == 2)
                {
                    index = 0;
                }
                else if (Event.current.button == 0)
                {
                    index = (index + 1) % list.Count;
                }
                else if (Event.current.button == 1)
                {
                    index = (index - 1) < 0 ? (list.Count - 1) : (index - 1);
                }

                LocalConfig.AutoMergeMode = list[index];
            },
            () => "Auto merge mode: " + LocalConfig.AutoMergeMode));


            Add(new DebugMenuItem("Reset AutoPlay", () =>
            {
                LocalConfig.AutoReloadMode = AutoReloadMode.None;
                LocalConfig.AutoSpawnMode = AutoSpawnMode.None;
                LocalConfig.AutoMergeMode = AutoMergeMode.None;
            }));
        }
    }
}
#endif