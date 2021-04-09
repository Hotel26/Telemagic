using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Telemagic {
    public class Hub {
    	static double Max_airport_radius = 1500;
    	static int Max_alt_diff = 10;
    	static double Max_refuel_radius = 30;
    	static double Min_major_runway = 995;
    	static double Min_minor_runway = 795;

    	/*  A cache of information about the current Vessel is kept here.
    	*/
    	static Group tower;
    	static String name;
    	static double lon, lat;
    	static String disqual;

    	/*  Checklist:
    	 
    	 		- landed
    	 		- cupola, Kerbal, >= 8m
    	 		- no wheels, docks
    	 		- 4 flags within 1.5km, all alt within 5m
    	 		- major: 1-1.2km
    	 		- minor: 0.8-1.2km
    	 		- inter: >= 30d
    	*/
    	public static bool check_airport(Vessel tower) {
    		// check T is Landed
    		if (!tower.Landed) return false;
    		// has cupola, Kerbal, no wheels, no docks
    		if (!has_cupola_etc(tower)) return false;
    		double alt = tower.altitude;
    		double min_alt = alt;
    		double max_alt = alt;

    		/*  find all flags within 1.5 km. 4 exactly.
    		 		all within 10m in altitude.
    		 		with A:
    		 		find a pair, AB, with distance between 1 and 1.2 km
    		 		other pair should be 0.8 to 1.2 km
    		 		angle >= 30d
    		 		intersecting
    		 		if this fails with A, repeat with C
    		*/
    		double tlon = tower.longitude;
    		double tlat = tower.latitude;
    		var neighbors = find_locale(tower.mainBody, tlon, tlat, Max_airport_radius);
    		Vessel[] flags = new Vessel[4];
    		int fx = 0;
    		// check flags
    		foreach (var ngbr in neighbors) {
    			var type = ngbr.RevealType();
    			if (type != "Flag") continue;
    			String fname = ngbr.name;
    			///if (!check_flag_placed(ngbr)) continue;
    			if (fx >= 4) {
    				disqual = "more then 4 flags in vicinity";
    				return false;
    			}
    			flags[fx++] = ngbr;
    			if (ngbr.altitude < min_alt) min_alt = ngbr.altitude;
    			else if (ngbr.altitude > max_alt) max_alt = ngbr.altitude;
    		}
    		if (fx != 4) {
    			disqual = "less than 4 flags in vicinity";
    			return false;
    		}
    		if (min_alt < max_alt - Max_alt_diff) {
    			disqual = "tower and flags must be within 10m alt of each other";
    			return false;
    		}
    		Vessel A = flags[0];
    		Vessel B = flags[1];
    		Vessel C = flags[2];
    		Vessel D = flags[3];
    		return
    			check_runways(A, B, C, D) ||
    			check_runways(A, C, B, D) ||
    			check_runways(A, D, B, C);
    	}

    	static bool check_flag_placed(Vessel flag) {
    		foreach (var part in flag.Parts) {
    			var pname = part.name;
    			//Telemagic.logTM($"{flag.vesselName} type {flag.GetType()} has pname {pname}");
    			if (pname == "flag") {
    				return true;
    				Telemagic.logTM($"{display(part)}");
    				// find MODULE with name = FlagSite
    				foreach (var module in part.Modules) {
    					// and state = Placed
    					Telemagic.logTM($"flag part module {part.name}");
    					//if (module.name == "FlagSite") {
    					//	bool placed = module.find("state").value("Placed");
    					//	Telemagic.logTM("checking flag " + flag.name + ' ' + placed);
    					//	return placed;
    					//}
    				}
    			}
    		}
    		return false;
    	}

    	static bool check_runways(Vessel A, Vessel B, Vessel C, Vessel D) {
    		double radius = A.mainBody.Radius;
    		double len1 = gcdist(radius, A.longitude - B.longitude, A.latitude, B.latitude);
    		double len2 = gcdist(radius, C.longitude - D.longitude, C.latitude, D.latitude);
    		if (!(len1 >= Min_major_runway && len2 >= Min_minor_runway ||
    		      len1 >= Min_minor_runway && len2 >= Min_major_runway))
    		{
    			disqual = "runways not long enough";
    			return false;
    		}
    		double AB = gcbear(A.longitude - B.longitude, A.latitude, B.latitude);
    		double CD = gcbear(C.longitude - D.longitude, C.latitude, D.latitude);
    		double diff = AB - CD;
    		if (diff < 0) diff = -diff;
    		if (diff > -30 && diff < 30 || diff > 150 && diff < 210)
    		{
    			disqual = "runways must intersect at >= 30 degrees";
    			return false;
    		}
    		return true;
    	}

    	public static string display(object obj) {
    		var sb = new StringBuilder();
    		sb.Append("{");
    		foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj)) {
    			string name = descriptor.Name;
    			object value = descriptor.GetValue(obj);
    			sb.Append($" {name} = {value},");
    		}
    		sb.Append(" }");
    		return sb.ToString();
    	}

    	/*  Returns a List of Vessels located within a radius of this point.
    	*/
    	static List<Vessel> find_locale(CelestialBody body, double lon, double lat, double radius) {
    		List<Vessel> vessels = new List<Vessel>();
    		foreach (var vessel in FlightGlobals.Vessels) {
    			if (!vessel.Landed) continue;
    			if (vessel.mainBody != body) continue;
    			double arc = 180 * radius / Math.PI / body.Radius;
    			//System.out.println("radius " + radius + " on ref:" + ref + " is " + arc);
    			double vlon = vessel.longitude;
    			double vlat = vessel.latitude;
    			double dist = gcdist(body.Radius, lon - vlon, lat, vlat);
    			if (dist < radius) vessels.Add(vessel);
    		}
    		return vessels;
    	}
    	
    	static double gcbear(double dlon, double lat1, double lat2) {
    		double rlon = toRadians(-dlon);
    		double rlat1 = toRadians(lat1);
    		double rlat2 = toRadians(lat2);
    		double S = Math.Cos(rlat2) * Math.Sin(rlon);
    		double C = Math.Cos(rlat1) * Math.Sin(rlat2) -
    		           Math.Sin(rlat1) * Math.Cos(rlat2) * Math.Cos(rlon);
    		double theta = Math.Atan2(S, C);
    		if (theta < 0) theta += 2 * Math.PI;
    		return 180 * theta / Math.PI;
    	}

    	/*  Compute the great circle distance between two points
    	*/
    	static double gcdist(double radius, double dlon, double lat1, double lat2)
    	{
    		double rlon = toRadians(dlon);
    		double rlat1 = toRadians(lat1);
    		double rlat2 = toRadians(lat2);
    		double dsigma =
    			Math.Acos(
    				Math.Sin(rlat1) * Math.Sin(rlat2) +
    				Math.Cos(rlat1) * Math.Cos(rlat2) * Math.Cos(rlon));
    		return radius * dsigma;
    	}

    	static bool has_cupola_etc(Vessel tower) {

    		/*  May not be immersed in water.  Therefore, require the altitude > 0.
    		*/
    		double alt = tower.altitude;
    		if (alt <= 0) return false;

    		/*  The cupola should be >= 8m above the ground, but the best we can do is
    			require the CoM to be >= 3m above the ground.
    		*/
    		Vector3 CoM = tower.CoM;
    		double halfHeight = Math.Abs(CoM.y);
    		//Telemagic.logTM($"bug: hhgt {halfHeight}");
    		///if (halfHeight < 3) return false;

    		bool cupola = false;
    		foreach (var part in tower.parts) {
    			if (part.name == "cupola") {
    				// check crew
    				//Telemagic.logTM($"cupola part identified");
    				if (part.protoModuleCrew.Count > 0) {
    					cupola = true;
    				} else disqual = "cupola has no crew";
    			} else if (part.name.Contains("Gear")) {
    				disqual = "wheels not permitted";
    				return false;
    			} else if (part.name.Contains("docking")) {
    				disqual = "docking port not permitted";
    				return false;
    			}
    		}
    		return cupola;
    	}

    	/*  First step is to consider every vessel within Max_refuel_radius and
    		determine whether it is a hub airport control tower.
    		[kmk] disable this           
    	*/
    	public static void refuel_hub_airport(Vessel vessel) {
    		disqual = null;

    		/*  Find all vessels within the refueling apron radius.  One of them may be a hub control tower.
    			In that case, we can refuel.
    		*/
    		List<Vessel> nearby = find_locale(vessel.mainBody, vessel.longitude, vessel.latitude, Max_refuel_radius);
    		foreach (var tower in nearby) {
    			if (check_airport(tower)) {
    				Telemagic.logTM($"refueling {vessel.vesselName}");
    				Telemagic.refuel(vessel);
    				return;
    			}
    		}
    		if (disqual != null) Telemagic.message(vessel, $"{disqual} at {name}");
    	}

    	static double toRadians(double deg) {
    		return deg * Math.PI / 180;
    	}
    }
}