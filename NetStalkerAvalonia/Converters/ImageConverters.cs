﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetStalkerAvalonia.Converters
{
	public class ImageConverters
	{
		public static ImagePathToImageConverter FromPath => ImagePathToImageConverter.Instance;
	}
}
