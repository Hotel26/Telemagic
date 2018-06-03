using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class TelemagicFueler : MonoBehaviour {
	public void Update() {
		var vessel = GetComponent<Vessel>();

		if (vessel == FlightGlobals.ActiveVessel) {

			/*  Cycle through all resources and replensih them.

				Cancel fueling when all full or if brakes are released or engines started.
			*/
			if (vessel.parts != null) {
				if (!Telemagic.enginesRunning(vessel) && Telemagic.brakesApplied(vessel)) {
					bool fueling = false;
					foreach (var part in vessel.parts) {
						int nres = part.Resources.Count;
						for (int i = 0; i < nres; ++i) {
							PartResource resource = part.Resources[i];
							if (resource.flowState) {       // not if flow is locked off
								var remAmt = resource.maxAmount - resource.amount;
								if (remAmt > 0) {
									var qty = resource.maxAmount * Time.deltaTime / 10;
									if (qty >= remAmt) {
										qty = remAmt;
									} else fueling = true;
									part.TransferResource(resource.info.id, qty);
								}
							}
						}
					}
					if (fueling) return;
				}
				Telemagic.message(vessel, "refueling complete");
			}
		}
		// finished
		Destroy(this);
	}
}
