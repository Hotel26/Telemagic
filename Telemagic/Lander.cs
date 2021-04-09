/*  As best can be determined and quite speculatively, a vessel on rails is, for convenience, in "orbit".

	This is not too hard to conceive if you consider its motion to be perfectly
	circular about the body's rotational "spindle".  I.e., the center of this
	rotation is not a gravitational 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using Telemagic;

namespace Telemagic {

    /*  The idea here originated in HyperEdit.  Using a gradual lander did not fix the "shakes"
    	phenomenon, so this is technicall ya no-op, particularly because, in the Telemagic scenario,
    	the exact height of ther terrain (at Baikerbanur) is precisely known.
    */
    public class TelemagicLander : MonoBehaviour
    {
    	public bool AlreadyTeleported { get; set; }
    	public Action<double, double, double, CelestialBody> OnManualEdit { get; set; }
    	public CelestialBody Body { get; set; }
    	public double Latitude { get; set; }
    	public double Longitude { get; set; }
    	public double Altitude { get; set; }
    	public bool SetRotation { get; set; }
    	public double InterimAltitude { get; set; }

    	private readonly object _accelLogObject = new object();
    	private bool teleportedToLandingAlt = false;
    	private double lastUpdate = 0;
    	//private double altAGL = 0; // Need to work out these in relation
    	//private double altASL = 0; // to land or sea.

    	/// <summary>
    	/// Sets the vessel altitude to the current calculation.
    	/// </summary>
    	public void SetAltitudeToCurrent() {
    		var pqs = Body.pqsController;
    		if (pqs == null) {
    			Destroy(this);
    			return;
    		}
    		var alt = pqs.GetSurfaceHeight(
    			          QuaternionD.AngleAxis(Longitude, Vector3d.down) *
    			          QuaternionD.AngleAxis(Latitude, Vector3d.forward) * Vector3d.right) -
    		          pqs.radius;
    		Telemagic.logTM("SetAltitudeToCurrent:: alt (pqs.GetSurfaceHeight) = " + alt);

    		alt = Math.Max(alt, 0); // Underwater

    		Altitude = GetComponent<Vessel>().altitude - alt;

    		Telemagic.logTM("SetAltitudeToCurrent::");
    		Telemagic.logTM(" alt = Math.Max(alt, 0) := " + alt);
    		Telemagic.logTM(" <Vessel>.altitude      := " + Altitude);

    	}

    	public void Update() {
    		if (TimeWarp.CurrentRateIndex != 0) {
    			TimeWarp.SetRate(0, true);
    			Telemagic.logTM("cancel time warp");
    		}
    	}

    	public void FixedUpdate() {
    		var vessel = GetComponent<Vessel>();

    		if (vessel != FlightGlobals.ActiveVessel) {
    			Destroy(this);
    			return;
    		}

    		if (TimeWarp.CurrentRateIndex != 0) {
    			TimeWarp.SetRate(0, true);
    			Telemagic.logTM("cancel time warp");
    		}

    		if (AlreadyTeleported) {

    			if (vessel.LandedOrSplashed) {
    				Destroy(this);
    			} else {
    				var accel = (vessel.srf_velocity + vessel.upAxis) * -0.5;
    				vessel.ChangeWorldVelocity(accel);
    			}
    		} else {
    			//NOT AlreadyTeleported
    			//Still calculating
    			var pqs = Body.pqsController;
    			if (pqs == null) { // The sun has no terrain.  Everthing else has a PQScontroller.
    				Destroy(this);
    				return;
    			}

    			var alt = pqs.GetSurfaceHeight(Body.GetRelSurfaceNVector(Latitude, Longitude)) - Body.Radius;
    			var tmpAlt = Body.TerrainAltitude(Latitude, Longitude);

    			double landHeight = FlightGlobals.ActiveVessel.altitude - FlightGlobals.ActiveVessel.pqsAltitude;

    			double finalAltitude = 0.0;

    			var checkAlt = FlightGlobals.ActiveVessel.altitude;
    			var checkPQSAlt = FlightGlobals.ActiveVessel.pqsAltitude;
    			double terrainAlt = GetTerrainAltitude();

    			Telemagic.logTM("-------------------");
    			Telemagic.logTM($"m1. Body.Radius  = {Body.Radius}");
    			Telemagic.logTM("m2. PQS SurfaceHeight = " + pqs.GetSurfaceHeight(Body.GetRelSurfaceNVector(Latitude, Longitude)));
    			Telemagic.logTM("alt ( m2 - m1 ) = " + alt);
    			Telemagic.logTM("Body.TerrainAltitude = " + tmpAlt);
    			Telemagic.logTM("checkAlt    = " + checkAlt);
    			Telemagic.logTM("checkPQSAlt = " + checkPQSAlt);
    			Telemagic.logTM("landheight  = " + landHeight);
    			Telemagic.logTM("terrainAlt  = " + terrainAlt);
    			Telemagic.logTM("-------------------");
    			Telemagic.logTM($"Latitude: {Latitude} Longitude: {Longitude}");
    			Telemagic.logTM("-------------------");

    			alt = Math.Max(alt, 0d);

    			// HoldVesselUnpack is in display frames, not physics frames

    			Vector3d teleportPosition;

    			if (!teleportedToLandingAlt) {
    				Telemagic.logTM("teleportedToLandingAlt == false");
    				Telemagic.logTM("interimAltitude: " + InterimAltitude);
    				Telemagic.logTM("Altitude: " + Altitude);

    				if (InterimAltitude > Altitude) {

    					if (Planetarium.GetUniversalTime() - lastUpdate >= 0.5) {
    						InterimAltitude = InterimAltitude / 10;
    						terrainAlt = GetTerrainAltitude();

    						if (InterimAltitude < terrainAlt) {
    							InterimAltitude = terrainAlt + Altitude;
    						}

    						//InterimAltitude = terrainAlt + Altitude;

    						teleportPosition = Body.GetWorldSurfacePosition(Latitude, Longitude, InterimAltitude) - Body.position;

    						Telemagic.logTM("1. teleportPosition = " +  teleportPosition);
    						Telemagic.logTM("1. interimAltitude: " + InterimAltitude);

    						if (lastUpdate != 0) {
    							InterimAltitude = Altitude;
    						}
    						lastUpdate = Planetarium.GetUniversalTime();

    					} else {
    						Telemagic.logTM("teleportPositionAltitude (no time change):");

    						teleportPosition = Body.GetWorldSurfacePosition(Latitude, Longitude, alt + InterimAltitude) - Body.position;

    						Telemagic.logTM("2. teleportPosition = " + teleportPosition);
    						Telemagic.logTM("2. alt: " + alt);
    						Telemagic.logTM("2. interimAltitude: " + InterimAltitude);
    					}
    				} else {
    					//InterimAltitude <= Altitude
    					Telemagic.logTM("3. teleportedToLandingAlt sets to true");

    					landHeight = FlightGlobals.ActiveVessel.altitude - FlightGlobals.ActiveVessel.pqsAltitude;
    					terrainAlt = GetTerrainAltitude();

    					//trying to find the correct altitude here.

    					if (checkPQSAlt > terrainAlt) {
    						alt = checkPQSAlt;
    					} else {
    						alt = terrainAlt;
    					}

    					if (alt == 0.0) {
    						//now what?
    					}

    					teleportedToLandingAlt = true;
    					//finalAltitude = alt + Altitude;
    					if (alt < 0) {
    						finalAltitude = Altitude;
    					} else if (alt > 0) {

    						finalAltitude = alt + Altitude;
    					} else {
    						finalAltitude = alt + Altitude;
    					}

    					teleportPosition = Body.GetWorldSurfacePosition(Latitude, Longitude, finalAltitude) - Body.position;

    					Telemagic.logTM("3. teleportPosition = " + teleportPosition);
    					Telemagic.logTM($"3. alt = {alt} Altitude = {Altitude} InterimAltitude = {InterimAltitude}");
    					Telemagic.logTM($"3. TerrainAlt = {terrainAlt} landHeight = {landHeight}");
    				}
    			} else {
    				Telemagic.logTM("teleportedToLandingAlt == true");

    				landHeight = FlightGlobals.ActiveVessel.altitude - FlightGlobals.ActiveVessel.pqsAltitude;
    				terrainAlt = GetTerrainAltitude();

    				Telemagic.logTM($"4. finalAltitude = {finalAltitude}");

    				//finalAltitude = alt + Altitude;
    				if (alt < 0) {
    					finalAltitude = Altitude;
    				} else if (alt > 0) {
    					finalAltitude = alt + Altitude;
    				} else {
    					finalAltitude = alt + Altitude;
    				}

    				//teleportPosition = Body.GetRelSurfacePosition(Latitude, Longitude, finalAltitude);
    				teleportPosition = Body.GetWorldSurfacePosition(Latitude, Longitude, finalAltitude) - Body.position;

    				Telemagic.logTM("4. teleportPosition = " + teleportPosition);
    				Telemagic.logTM($"4. alt = {alt} Altitude = { Altitude} InterimAltitude = { InterimAltitude}");
    				Telemagic.logTM($"4. TerrainAlt = {terrainAlt} landHeight = {landHeight}");
    				Telemagic.logTM("4. finalAltitude = " + finalAltitude);
    			}

    			var teleportVelocity = Vector3d.Cross(Body.angularVelocity, teleportPosition);

    			// convert from world space to orbit space

    			teleportPosition = teleportPosition.xzy;
    			teleportVelocity = teleportVelocity.xzy;

    			Telemagic.logTM("0. teleportPosition(xzy): " +  teleportPosition);
    			Telemagic.logTM("0. teleportVelocity(xzy): " +  teleportVelocity);
    			Telemagic.logTM("0. Body                 : " +  Body);

    			// counter for the momentary fall when on rails (about one second)
    			teleportVelocity += teleportPosition.normalized * (Body.gravParameter / teleportPosition.sqrMagnitude);

    			Quaternion rotation;


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

    			var orbit = Telemagic.Clone(vessel.orbitDriver.orbit);
    			orbit.UpdateFromStateVectors(teleportPosition, teleportVelocity, Body, Planetarium.GetUniversalTime());

    			Telemagic.SetOrbit(vessel, orbit);
    			vessel.SetRotation(rotation);

    			if (teleportedToLandingAlt) {
    				AlreadyTeleported = true;
    				Telemagic.logTM(" :FINISHED TELEPORTING:");
    			}
    		}
    	}

    	/* <see cref="https://github.com/KSP-KOS/KOS/blob/develop/src/kOS/Suffixed/GeoCoordinates.cs"/> */
    	public Double GetTerrainAltitude() {
    		double alt = 0.0;
    		PQS bodyPQS = Body.pqsController;
    		if (bodyPQS != null) // The sun has no terrain.  Everything else has a PQScontroller.
    		{
    			// The PQS controller gives the theoretical ideal smooth surface curve terrain.
    			// The actual ground that exists in-game that you land on, however, is the terrain
    			// polygon mesh which is built dynamically from the PQS controller's altitude values,
    			// and it only approximates the PQS controller.  The discrepancy between the two
    			// can be as high as 20 meters on relatively mild rolling terrain and is probably worse
    			// in mountainous terrain with steeper slopes.  It also varies with the user terrain detail
    			// graphics setting.

    			// Therefore the algorithm here is this:  Get the PQS ideal terrain altitude first.
    			// Then try using RayCast to get the actual terrain altitude, which will only work
    			// if the LAT/LONG is near the active vessel so the relevant terrain polygons are
    			// loaded.  If the RayCast hit works, it overrides the PQS altitude.

    			// PQS controller ideal altitude value:
    			// -------------------------------------

    			// The vector the pqs GetSurfaceHeight method expects is a vector in the following
    			// reference frame:
    			//     Origin = body center.
    			//     X axis = LATLNG(0,0), Y axis = LATLNG(90,0)(north pole), Z axis = LATLNG(0,-90).
    			// Using that reference frame, you tell GetSurfaceHeight what the "up" vector is pointing through
    			// the spot on the surface you're querying for.
    			var bodyUpVector = new Vector3d(1, 0, 0);
    			bodyUpVector = QuaternionD.AngleAxis(Latitude, Vector3d.forward/*around Z axis*/) * bodyUpVector;
    			bodyUpVector = QuaternionD.AngleAxis(Longitude, Vector3d.down/*around -Y axis*/) * bodyUpVector;

    			alt = bodyPQS.GetSurfaceHeight(bodyUpVector) - bodyPQS.radius;

    			// Terrain polygon raycasting:
    			// ---------------------------
    			const double HIGH_AGL = 1000.0;
    			const double POINT_AGL = 800.0;
    			const int TERRAIN_MASK_BIT = 15;

    			// a point hopefully above the terrain:
    			Vector3d worldRayCastStart = Body.GetWorldSurfacePosition(Latitude, Longitude, alt + HIGH_AGL);
    			// a point a bit below it, to aim down to the terrain:
    			Vector3d worldRayCastStop = Body.GetWorldSurfacePosition(Latitude, Longitude, alt + POINT_AGL);
    			RaycastHit hit;
    			if (Physics.Raycast(worldRayCastStart, (worldRayCastStop - worldRayCastStart), out hit, float.MaxValue, 1 << TERRAIN_MASK_BIT)) {
    				// Ensure hit is on the topside of planet, near the worldRayCastStart, not on the far side.
    				if (Mathf.Abs(hit.distance) < 3000) {
    					// Okay a hit was found, use it instead of PQS alt:
    					alt = ((alt + HIGH_AGL) - hit.distance);
    				}
    			}
    		}
    		return alt;
    	}
    }
}