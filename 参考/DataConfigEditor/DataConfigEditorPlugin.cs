#if TOOLS
using Godot;

namespace Slime.Addons.DataConfigEditor
{
    [Tool]
    public partial class DataConfigEditorPlugin : EditorPlugin
    {
        private ConfigTablePanel? _panel;

        public override void _EnterTree()
        {
            _panel = new ConfigTablePanel
            {
                Name = "DataConfigEditorPanel",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };

            var mainScreen = EditorInterface.Singleton.GetEditorMainScreen();
            mainScreen.AddChild(_panel);

            _panel.Visible = false;
        }

        public override void _ExitTree()
        {
            if (_panel != null)
            {
                _panel.QueueFree();
                _panel = null;
            }
        }

        public override bool _HasMainScreen()
        {
            return true;
        }

        public override void _MakeVisible(bool visible)
        {
            if (_panel != null)
                _panel.Visible = visible;
        }

        public override string _GetPluginName()
        {
            return "DataConfig";
        }

        public override Texture2D? _GetPluginIcon()
        {
            return EditorInterface.Singleton.GetBaseControl().GetThemeIcon("Grid", "EditorIcons");
        }
    }
}
#endif
