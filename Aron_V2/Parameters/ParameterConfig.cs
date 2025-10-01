using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Aron_V2
{
	[XmlRoot("AppConfig")]
	public class AppConfig
	{
		[XmlArray("Models"), XmlArrayItem("Model")]
		public List<ModelConfig> Models { get; set; }
	}

	public class ModelConfig
	{
		[XmlAttribute("Name")]
		public string Name { get; set; }

		public GeneralConfig General { get; set; }

		[XmlArray("Cameras"), XmlArrayItem("Camera")]
		public List<CameraConfig> Cameras { get; set; }
	}

	public class GeneralConfig
	{
		public string maxLines_Richbox { get; set; }
		public string CamN { get; set; }

	}

	public class CameraConfig
	{
		[XmlAttribute("Name")]
		public string Name { get; set; }

		[XmlArray("Positions"), XmlArrayItem("Position")]
		public List<PositionConfig> Positions { get; set; }
	}

	public class PositionConfig
	{
		[XmlAttribute("Name")]
		public string Name { get; set; }

		// 新增：相机自己的 General，可以没有
		[XmlElement("General")]
		public PositionGeneralConfig General { get; set; }

		[XmlArray("Parameters"), XmlArrayItem("Parameter")]
		public List<ParameterConfig> Parameters { get; set; }
	}

	public class ParameterConfig
	{
		[XmlAttribute("Name")]
		public string Name { get; set; }

		[XmlAttribute("Description")]
		public string Description { get; set; }

		[XmlText]
		public string Value { get; set; }
	}

	public class PositionGeneralConfig
	{
		public string Exposure { get; set; }      // "10"
		public string MainUsed { get; set; }      // "1"
		public string MainChannel { get; set; }
		public string SecondUsed { get; set; }    // "0"
		public string SecondChannel { get; set; } // "0"
	}
}
