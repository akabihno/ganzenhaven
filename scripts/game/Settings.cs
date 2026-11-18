using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Player settings that outlive a playthrough: music and sound-effect volume, and whether
/// lines are voiced. Kept in user://settings.cfg (next to the language, see
/// <see cref="GameState"/>) and applied to the audio buses in default_bus_layout.tres.
/// </summary>
public static class Settings
{
	private const string Path = "user://settings.cfg";
	private const string Section = "audio";

	/// <summary>0–1, applied to the Music bus.</summary>
	public static float MusicVolume { get; private set; } = 1f;

	/// <summary>0–1, applied to the Sfx bus (cutscene sounds).</summary>
	public static float SfxVolume { get; private set; } = 1f;

	/// <summary>Whether dialogue lines play their recordings.</summary>
	public static bool VoiceOn { get; private set; } = true;

	/// <summary>Something changed (so an open screen can update its controls).</summary>
	public static event Action Changed;

	// The level each bus has in the layout; the sliders scale down from there.
	private static readonly Dictionary<string, float> LayoutDb = new();

	public static void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(Path) == Error.Ok)
		{
			MusicVolume = Mathf.Clamp((float)cfg.GetValue(Section, "music", 1f), 0f, 1f);
			SfxVolume = Mathf.Clamp((float)cfg.GetValue(Section, "sfx", 1f), 0f, 1f);
			VoiceOn = (bool)cfg.GetValue(Section, "voice", true);
		}
		ApplyBus("Music", MusicVolume);
		ApplyBus("Sfx", SfxVolume);
	}

	public static void SetMusicVolume(float value, bool save = true)
	{
		MusicVolume = Mathf.Clamp(value, 0f, 1f);
		ApplyBus("Music", MusicVolume);
		Done(save);
	}

	public static void SetSfxVolume(float value, bool save = true)
	{
		SfxVolume = Mathf.Clamp(value, 0f, 1f);
		ApplyBus("Sfx", SfxVolume);
		Done(save);
	}

	public static void SetVoiceOn(bool on)
	{
		VoiceOn = on;
		Done(true);
	}

	/// <summary>Writes the current values (sliders call this when let go, not on every step).</summary>
	public static void Save()
	{
		var cfg = new ConfigFile();
		cfg.Load(Path);   // keep the language and anything else in there
		cfg.SetValue(Section, "music", MusicVolume);
		cfg.SetValue(Section, "sfx", SfxVolume);
		cfg.SetValue(Section, "voice", VoiceOn);
		cfg.Save(Path);
	}

	private static void Done(bool save)
	{
		if (save)
			Save();
		Changed?.Invoke();
	}

	private static void ApplyBus(string bus, float volume)
	{
		int index = AudioServer.GetBusIndex(bus);
		if (index < 0)
			return;
		if (!LayoutDb.TryGetValue(bus, out float layout))
			LayoutDb[bus] = layout = AudioServer.GetBusVolumeDb(index);
		// Squared, so the slider's halfway point sounds about half as loud rather than barely quieter.
		AudioServer.SetBusVolumeDb(index, layout + Mathf.LinearToDb(Mathf.Max(volume * volume, 0.0001f)));
		AudioServer.SetBusMute(index, volume <= 0.001f);
	}
}
