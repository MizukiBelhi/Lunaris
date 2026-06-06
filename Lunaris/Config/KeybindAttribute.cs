using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Lunaris.Config
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class KeybindAttribute(params KeyCode[] keys) : Attribute
	{
		public KeyCode[] Keys { get; } = keys;
	}
}
