using System.Runtime.CompilerServices;

namespace VSPackage.CPPCheckPlugin
{
	public class AtomicBool
	{
		public AtomicBool()
		{
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public AtomicBool(bool value)
		{
			_value = value;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static implicit operator bool(AtomicBool a)
		{
			return a._value;
		}

		public bool Value
		{
			[MethodImpl(MethodImplOptions.Synchronized)]
			get => _value;
			[MethodImpl(MethodImplOptions.Synchronized)]
			set { _value = value; }
		}

		private bool _value = false;
	}
}
