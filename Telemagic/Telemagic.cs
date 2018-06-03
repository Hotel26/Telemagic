﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEngine;
using UnityEngine.UI;
using KSP.UI.Screens;
using ModuleWheels;
using VehiclePhysics;

[assembly: System.Reflection.AssemblyProduct("Telemagic")]
[assembly: System.Reflection.AssemblyTitle("Telemagic")]
[assembly: System.Reflection.AssemblyDescription("KSP Teleporter")]
[assembly: System.Reflection.AssemblyCopyright("Hotel26")]
[assembly: System.Reflection.AssemblyVersion("1.3.1.8")]

[KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
public class Telemagic : MonoBehaviour
{
	public const string TM_version = "1.3.1.8";
	static ApplicationLauncherButton TMbutton;
	static string plugdir = Path.GetFullPath(Path.Combine(typeof(Telemagic).Assembly.Location, ".."));

	/*  Called at scene load before Start().
	*/
	public void Awake()
	{
		// Called after scene (designated w/ KSPAddon) loads, but before Start().  Init data here.
		addTMbutton();
	}

	void Start() {
		// following needed to fix a stock bug
		///appListModHidden = (List<ApplicationLauncherButton>)typeof(ApplicationLauncher).GetField("appListModHidden", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ApplicationLauncher.Instance);
		DontDestroyOnLoad(this);
	}

	void FixedUpdate() {
	}

	ApplicationLauncherButton addTMbutton() {
		if (Versioning.version_major != 1 || Versioning.version_minor != 3) {
			logTM($"Telemagic {TM_version} only supports KSP v1.3.x");
			return null;
		}
		if (TMbutton != null) return TMbutton;	// already done

		var applauncher = ApplicationLauncher.Instance;
		if (applauncher == null) {
			logTM("Cannot add to ApplicationLauncher, instance was null");
			return null;
		}

		var pngpath = Path.Combine(plugdir, "Telemagic.png");
		logTM($"attempt to load TM button icon {pngpath}");
		var img = loadTMButtonImage(pngpath);
		TMbutton = applauncher.AddModApplication(
			doTelemagic, // onTrue
			doTelemagic, // onFalse
			() => { }, // onHover
			() => { }, // onHoverOut
			() => { }, // onEnable
			() => { }, // onDisable
			ApplicationLauncher.AppScenes.FLIGHT, // visibleInScenes
			img // texture
		);
		return TMbutton;
	}

	public static bool brakesApplied(Vessel vessel) {
		if (vessel.parts == null) return false;
		foreach (var part in vessel.parts) {
			int nmod = part.Modules.Count;
			for (int i = 0; i < nmod; ++i) {
				var brakes = part.Modules[i] as ModuleWheelBrakes;
				if (brakes != null && brakes.brakeInput == 0) return false;
			}
		}
		return true;
	}

	/*  There is a new(?) class called VesselTeleporter.
	*/
	Vessel check_location(double lat, double lon) {
		if (FlightGlobals.fetch == null) return null;
		var radius = 0.003;
		var landed = FlightGlobals.Vessels.Where(v => v.Landed);
		foreach (var vessel in landed) {
			//logTM($"vessel {vessel.vesselName} on {vessel.mainBody.bodyName} landed {vessel.Landed} at {vessel.latitude} {vessel.longitude}");
			if (vessel.mainBody.bodyName != "Kerbin") continue;
			if (vessel.latitude < lat - radius || vessel.latitude > lat + radius) continue;
			if (vessel.longitude < lon - radius || vessel.longitude > lon + radius) continue;
			return vessel;
		}
		return null;
	}

	void doTelemagic() {
		var vessel = FlightGlobals.ActiveVessel;

		/*  Do preliminary checks.
		*/
		if (TimeWarp.CurrentRateIndex != 0) { message(vessel, "is being time warped"); return; }
		if (!vessel.Landed) { message(vessel, "is not landed"); return; }
		if (!brakesApplied(vessel)) { message(vessel, "brakes must be applied"); return; }
		if (enginesRunning(vessel)) { message(vessel, "engines are not shutdown"); return; }

		/*  There are 3 cases to handle:

			1. teleport to BKB
			2. refuel at KSC/BKB
			3. refuel when near a tower

			In all cases, the vessel must be stationary, parked, braked, engines shutdown,
			on the ground.
		*/
		var at = vessel.landedAt;
		if (at.IndexOf("Runway") >= 0) {
			teleport_BKB(vessel, false);
			return;
		} else if (at == "LaunchPad") {
			message(vessel, "teleporting to the Baikerbanur launchpad not yet supported");
			//teleport_BKB(vessel, true);
			return;
		}

		// refuel at KSC or BKB?
		var lon = vessel.longitude;
		var lat = vessel.latitude;
		if (lon >= -74.639967 && lon <= -74.638766 && lat >= -0.060918 && lat <= -0.057436
			|| lon >= -146.437 && lon <= -146.431 && lat >= 20.633 && lat <= 20.639)
		{
			refuel(vessel);
			return;
		}
		Hub.refuel_hub_airport(vessel);
	}

	public static bool enginesRunning(Vessel vessel) {
		if (vessel.parts == null) return false;
		foreach (var part in vessel.parts) {
			int nmod = part.Modules.Count;
			for (int i = 0; i < nmod; ++i) {
				var engine = part.Modules[i] as ModuleEngines;
				if (engine != null && engine.EngineIgnited && !engine.engineShutdown) return true;
			}
		}
		return false;
	}

	static Quaternion invert(Quaternion q) {
		return new Quaternion(q.w, -q.x, -q.y, -q.z);
	}

	static Texture2D loadTMButtonImage(string path) {
		Texture2D img = new Texture2D(38, 38, TextureFormat.RGBA32, false);
		//if (Versioning.version_minor > 3) {

		//	for (var x = 0; x < img.width; x++)
		//	for (var y = 0; y < img.height; y++)
		//		img.SetPixel(x, y,
		//			new Color(2 * (float)Math.Abs(x - img.width / 2) / img.width, 0.25f,
		//				2 * (float)Math.Abs(y - img.height / 2) / img.height, 0));

		//	var black = new Color(1, 1, 1);
		//	for (var y = 10; y < img.height - 10; y++) {
		//		img.SetPixel(y - 1, y, black);
		//		img.SetPixel(y, y, black);
		//		img.SetPixel(y + 1, y, black);
		//		img.SetPixel(38 - y - 1, y, black);
		//		img.SetPixel(38 - y, y, black);
		//		img.SetPixel(38 - y + 1, y, black);
		//	}

		//	img.Apply();
		//	return img;
		//}

		// our png has been loaded by KSP as a resource
		// but we found KSP said it was 'unreadable'
		var textureName = "Telemagic/Telemagic";

		// this is the KerbalKonstructs method
		//if (GameDatabase.Instance.ExistsTexture(textureName)) {
		//  // Get the texture URL
		//  img = GameDatabase.Instance.GetTexture(textureName, true);
		//	if (img != null) {
		//		logTM($"found Telemagic.png!");
		//		img.Apply();
		//		return img;
		//	}
		//}

		//img = AssetBase.GetTexture("Telemagic/Telemagic");
		//img.Apply();
		//return img;
		// but it comes back with no information!

		img = new Texture2D(38, 38, TextureFormat.RGBA32, false);
		var bytes = File.ReadAllBytes(path);
		img.LoadImage(bytes);

		// suggested by Bewing
		//img.LoadRawTextureData(bytes);
		//img.Apply();
		return img;
	}

	public static void logTM(string msg)
	{
		UnityEngine.Debug.Log("[Telemagic] " + msg);
	}

	public static bool message(Vessel vessel, string message) {
		ScreenMessages.PostScreenMessage($"{vessel.vesselName} {message}", 3);
		logTM(message);
		return false;
	}

	Quaternion mult2(Quaternion p, Quaternion q)
	{
		return new Quaternion(
			p.w * q.w - p.x * q.x - p.y * q.y - p.z * q.z,
			p.w * q.x + p.x * q.w + p.y * q.z - p.z * q.y,
			p.w * q.y - p.x * q.z + p.y * q.w + p.z * q.x,
			p.w * q.z + p.x * q.y - p.y * q.x + p.z * q.w
		);
	}

	Quaternion rotate(Quaternion p, Quaternion q)
	{
		Quaternion result = invert(p);
		result = mult2(mult2(p, q), result);
		return result;
	}

	internal static void refuel(Vessel vessel)
	{
		if (vessel.parts == null) return;

		logTM($"considering {vessel.vesselName} for refueling...");

		logTM($"{vessel.vesselName} is {vessel.RevealType()}");
		if (vessel.isEVA) {
			foreach (var part in vessel.parts) {
				logTM($"{part.name} is {part.GetType()}");
				if (part.name.Contains("kerbalEVA")) {
					logTM($"{Hub.display(part)}");
					foreach (var mod in part.Modules) {//vessel.vesselModules) {
						logTM($"mod {mod.name}");
					}
					foreach (var res in part.Resources) {
						logTM($"res {res.resourceName}");
					}
				}
			}
			logTM($"that's all");
		}
		// does the vessel have chutes?
		foreach (var part in vessel.parts) {
			if (part.name.Contains("chute")) {
				logTM($"chute: {part.name} mods:{part.Modules.Count}");
				foreach (var mod in part.Modules) {
					logTM($"mod {mod.name}");
					if (mod.name.Contains("chute")) {
						logTM($"chute module {Hub.display(mod)}");
						var chute = mod as ModuleParachute;
						if (chute != null) chute.Repack();
					}
				}
			} else {
				var crew = part.protoModuleCrew;
				foreach (var member in crew) {
					logTM($"{member.name} in {vessel.vesselName}");
					///member.
				}
			}
		}

		// check all Kerbals have at least one flag
		//var crew = vessel.GetVesselCrew();
		//foreach (var member in crew) {
		//	logTM($"{member.name} {Hub.display(member)}");
		//}
		//foreach (var part in vessel.parts) {
		//	var crew2 = part.protoModuleCrew;
		//	logTM($"in {part.name} crew {crew2.Count}");
		//}

		FlightGlobals.ActiveVessel.gameObject.AddComponent<TelemagicFueler>();
	}

	bool teleport_BKB(Vessel vessel, bool launchpad) {
		Quaternion ksc;
		Quaternion bkb;
		double lng2;
		double lat2;
		double alt2;
		Quaternion qrot;
		if (launchpad) {
			ksc = new Quaternion(-0.700913131f, -0.0918226093f, -0.0975999981f, 0.70054543f);
			bkb = new Quaternion(-0.407139033f, 0.172273681f, 0.403018743f, 0.801333606f);
			lng2 = -146.426444;
			lat2 = 20.6487555;
			alt2 = 424;
			qrot = new Quaternion(0.790337f, -0.572625f, -0.004792f, -0.217819f);
		} else {
			ksc = new Quaternion(0.093959339f, -0.0946838111f, -0.702652752f, 0.698917568f);
			bkb = new Quaternion(0.219796866f, -0.438783914f, 0.671991587f, -0.554603815f);
			lng2 = -146.6;
			lat2 = 20.6;
			alt2 = 424;
			qrot = new Quaternion(0.741916f, -0.085163f, 0.234213f, 0.622457f);
		}

		var name = vessel.name;
		var lon = vessel.longitude;
		var lat = vessel.latitude;
		var alt = vessel.altitude;
		var hgt = vessel.heightFromSurface;
		var rot = vessel.srfRelRotation;
		//String[] qElts = rot.value.split(",");

		/*  Ensure the teleport target is clear.
		*/
		Vessel other;
		if ((other = check_location(lat2, lng2)) != null) {
			if (launchpad) {
				/// display name of vessel
				//do {
				//	Attribute landed = (Attribute) vessel.find("landed");
				//	boolean debris = ((Attribute) other.find("type")).value("Debris");
				//	if (landed.value("True") && debris)
				//	{
				//		Attribute vsit = (Attribute) other.find("sit");
				//		vsit.value = "VAPORIZE";
				//	}
				//	else
				//	{
				//		System.out.println("BKB launchpad in use");
				//		return false;
				//	}
				//} while ((other = check_location(lng2, lat2)) != null);
				//message(vessel, "BKB launchpad debris cleared...");
				message(vessel, "BKB launchpad is obstructed");
			} else {

				/*  For the "runway" (grass strip), look for a clear spot in the flight
					line.
				*/
				do
				{
					lat2 -= 0.003; // step down the flight line
					if (lat2 < 20.447)
					{
						/// display name of vessel
						message(vessel, "BKB flight line is full");
						return false;
					}
				} while (check_location(lat2, lng2) != null);
			}
		}

		CelestialBody body = vessel.mainBody;

		///alt2 += 1.5;		// free fall

		var telePos = body.GetWorldSurfacePosition(lat2, lng2, alt2 + hgt) - body.position;
		var teleVel = Vector3d.Cross(body.angularVelocity, telePos);

		// convert from world space to orbit space
		telePos = telePos.xzy;
		teleVel = teleVel.xzy;
		// counter for the momentary fall when on rails (about one second)
		teleVel += telePos.normalized * (body.gravParameter / telePos.sqrMagnitude);

		var orbit = Clone(vessel.orbitDriver.orbit);
		orbit.UpdateFromStateVectors(telePos, teleVel, body, Planetarium.GetUniversalTime());

		vessel.Landed = false;

		var oldUp = vessel.orbit.pos.xzy.normalized;
		var newUp = telePos.xzy.normalized;
		qrot = Quaternion.FromToRotation(oldUp, newUp) * vessel.vesselTransform.rotation;

		SetOrbit(vessel, orbit);
		vessel.SetRotation(qrot);   // mult2(qrot, rot) [TM]

		// doing the following is a no-op (so far)
		//vessel.latitude = lat2;
		//vessel.longitude = lng2;
		vessel.altitude = alt2;

		message(vessel, $" teleported to Baikerbanur {lat2} {lng2} {alt2 + hgt}");

		var lander = FlightGlobals.ActiveVessel.gameObject.AddComponent<TelemagicLander>();
		lander.Latitude = lat2;
		lander.Longitude = lng2;
		lander.SetRotation = false;
		lander.Body = vessel.mainBody;
		lander.AlreadyTeleported = false;
		lander.SetAltitudeToCurrent();

		return true;
		/*  This is how HyperEdit does it:
        
			if (SetRotation) {
				// Need to check vessel and find up for the root command pod
				vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false); //hopefully this disables SAS as it causes unknown results!

				var from = Vector3d.up; //Sensible default for all vessels

				if (vessel.displaylandedAt == "Runway" || vessel.vesselType.ToString() == "Plane") {
					from = vessel.vesselTransform.up;
				}


				var to = teleportPosition.xzy.normalized;
				rotation = Quaternion.FromToRotation(from, to);
			} else {
				var oldUp = vessel.orbit.pos.xzy.normalized;
				var newUp = teleportPosition.xzy.normalized;
				rotation = Quaternion.FromToRotation(oldUp, newUp) * vessel.vesselTransform.rotation;
			}

			var orbit = vessel.orbitDriver.orbit.Clone();
			orbit.UpdateFromStateVectors(teleportPosition, teleportVelocity, Body, Planetarium.GetUniversalTime());
        
			vessel.SetOrbit(orbit);
			vessel.SetRotation(rotation);

		*/
	}

	public static void SetOrbit(Vessel vessel, Orbit newOrbit)
	{
		var destinationMagnitude = newOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()).magnitude;

		try
		{
			OrbitPhysicsManager.HoldVesselUnpack(60);
		}
		catch (NullReferenceException)
		{
			logTM($"{vessel} OrbitPhysicsManager.HoldVesselUnpack threw NullReferenceException");
		}

		var allVessels = FlightGlobals.fetch?.vessels ?? (IEnumerable<Vessel>)new[] { vessel };
		foreach (var v in allVessels) v.GoOnRails();

		var oldBody = vessel.orbitDriver.orbit.referenceBody;

		HardsetOrbit(vessel.orbitDriver, newOrbit);

		vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
		vessel.orbitDriver.vel = vessel.orbit.vel;

		var newBody = vessel.orbitDriver.orbit.referenceBody;
		if (newBody != oldBody)
		{
			var evnt = new GameEvents.HostedFromToAction<Vessel, CelestialBody>(vessel, oldBody, newBody);
			GameEvents.onVesselSOIChanged.Fire(evnt);
		}
	}

	private static void HardsetOrbit(OrbitDriver orbitDriver, Orbit newOrbit)
	{
		var orbit = orbitDriver.orbit;
		orbit.inclination = newOrbit.inclination;
		orbit.eccentricity = newOrbit.eccentricity;
		orbit.semiMajorAxis = newOrbit.semiMajorAxis;
		orbit.LAN = newOrbit.LAN;
		orbit.argumentOfPeriapsis = newOrbit.argumentOfPeriapsis;
		orbit.meanAnomalyAtEpoch = newOrbit.meanAnomalyAtEpoch;
		orbit.epoch = newOrbit.epoch;
		orbit.referenceBody = newOrbit.referenceBody;
		orbit.Init();
		orbit.UpdateFromUT(Planetarium.GetUniversalTime());
		if (orbit.referenceBody != newOrbit.referenceBody)
		{
			orbitDriver.OnReferenceBodyChange?.Invoke(newOrbit.referenceBody);
		}
	}

	public static Orbit Clone(Orbit o)
	{
		return new Orbit(o.inclination, o.eccentricity, o.semiMajorAxis, o.LAN,
			o.argumentOfPeriapsis, o.meanAnomalyAtEpoch, o.epoch, o.referenceBody);
	}
}