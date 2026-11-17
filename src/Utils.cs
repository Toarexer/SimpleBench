using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace SimpleBench;

internal readonly struct DeviceInfo(Device device, int index) {
	public Device Device { get; } = device;

	public string Id { get; } = $"{device.AcceleratorType}_{index}";

	public override string ToString() => $"{Id}: {Device.Name}";
}

internal static class Utils {
	public static IEnumerable<DeviceInfo> GetPreferredDevices() {
		using Context context = Context.CreateDefault();
		return GetPreferredDevices(context);
	}

	public static IEnumerable<DeviceInfo> GetPreferredDevices(Context context) {
		HashSet<string> names = [.. context.GetPreferredDevices(false, false).Select(device => device.Name)];
		foreach (DeviceInfo info in GetAvailableDevices(context))
			if (names.Contains(info.Device.Name))
				yield return info;
	}

	public static IEnumerable<DeviceInfo> GetAvailableDevices() {
		using Context context = Context.CreateDefault();
		return GetAvailableDevices(context);
	}

	public static IEnumerable<DeviceInfo> GetAvailableDevices(Context context) {
		foreach (DeviceInfo info in context.GetDeviceInfos<CPUDevice>())
			yield return info;
		foreach (DeviceInfo info in context.GetDeviceInfos<CLDevice>())
			yield return info;
		foreach (DeviceInfo info in context.GetDeviceInfos<CudaDevice>())
			yield return info;
	}

	private static DeviceInfo[] GetDeviceInfos<T>(this Context context) where T : Device {
		Context.DeviceCollection<T> devices = context.GetDevices<T>();
		DeviceInfo[] infos = new DeviceInfo[devices.Count];

		for (int i = 0; i < devices.Count; i++)
			infos[i] = new DeviceInfo(devices[i], i);

		return infos;
	}
}
