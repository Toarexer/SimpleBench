using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace SimpleBench;

internal record DeviceInfo(string Type, int Index, string Name);

internal static class Utils {
	public static IEnumerable<DeviceInfo> GetPreferredDevices() {
		using Context context = Context.CreateDefault();
		return GetPreferredDevices(context);
	}

	public static IEnumerable<DeviceInfo> GetPreferredDevices(Context context) {
		HashSet<string> names = [.. context.GetPreferredDevices(false, false).Select(device => device.Name)];
		foreach (DeviceInfo info in GetAvailableDevices(context))
			if (names.Contains(info.Name))
				yield return info;
	}

	public static IEnumerable<DeviceInfo> GetAvailableDevices() {
		using Context context = Context.CreateDefault();
		return GetAvailableDevices(context);
	}

	public static IEnumerable<DeviceInfo> GetAvailableDevices(Context context) {
		foreach (DeviceInfo info in context.GetDeviceNames<CPUDevice>())
			yield return info;
		foreach (DeviceInfo info in context.GetDeviceNames<CLDevice>())
			yield return info;
		foreach (DeviceInfo info in context.GetDeviceNames<CudaDevice>())
			yield return info;
	}

	private static DeviceInfo[] GetDeviceNames<T>(this Context context) where T : Device {
		Context.DeviceCollection<T> devices = context.GetDevices<T>();
		DeviceInfo[] infos = new DeviceInfo[devices.Count];

		for (int i = 0; i < devices.Count; i++)
			infos[i] = new DeviceInfo(typeof(T).Name, i, devices[i].Name);

		return infos;
	}
}
