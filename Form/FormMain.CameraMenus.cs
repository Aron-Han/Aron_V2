using Aron_V2.CameraIntegration;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class FormMain
    {
        private void InitializeCameraMenus()
        {
            // Keep the existing permission-controlled Camera parent item.
            cameraToolStripMenuItem.Click -= cameraToolStripMenuItem_Click;
            var visionpro = new ToolStripMenuItem("Visionpro") { Name = "cameraVisionproMenuItem" };
            visionpro.Click += cameraToolStripMenuItem_Click;
            cameraToolStripMenuItem.DropDownItems.Add(visionpro);
            foreach (string category in new[] { CameraAcquisition.Sdk3D, CameraAcquisition.Sdk2DLine, CameraAcquisition.Sdk2D })
            {
                string page = category;
                var item = new ToolStripMenuItem(category) { Name = "camera" + category.Replace("-", "") + "MenuItem" };
                item.Click += (s, e) => OpenVisionEditor(() => new SdkCameraForm(XmlConfigHelper.Load(Global.ParameterCogfig), page), "Camera " + page);
                cameraToolStripMenuItem.DropDownItems.Add(item);
            }
            cameraToolStripMenuItem.DropDownDirection = ToolStripDropDownDirection.Right;
        }
    }
}
