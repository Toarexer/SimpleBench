using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace SimpleBench;

/// <summary>Encapsulates a <see cref="Device"/> so it can be uniquely identified.</summary>
/// <param name="device">The device to encapsulate.</param>
/// <param name="index">The index of the device that must be different from the devices of the same <see cref="AcceleratorType"/>.</param>
internal readonly struct DeviceInfo(Device device, int index) {
	/// The ILGPU device.
	public Device Device { get; } = device;

	/// The unique identifier of the device made from the <see cref="AcceleratorType"/> and index.
	public string Id { get; } = $"{device.AcceleratorType}_{index}";

	/// <summary>Returns display name of the device derived from the <see cref="Id"/> and <see cref="Device.Name"/>.</summary>
	/// <returns>The display name of the device.</returns>
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
