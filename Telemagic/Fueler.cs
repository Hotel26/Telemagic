using UnityEngine;
using System;

namespace Telemagic {

    public class TelemagicFueler : MonoBehaviour {

        public const int refuelingDuration = 10;    // seconds
        public double timeRemaining;

        public Vessel vessel;
        public FuelTotals vesselFuelTotals;

        public Vessel sourceVessel;
        public FuelTotals sourceVesselFuelTotals;

        public Vessel BeginRefueling() {
            vessel = FlightGlobals.ActiveVessel;
            Telemagic.logTM($"current vessel is {vessel.vesselName}");
            vesselFuelTotals = new FuelTotals(vessel);
            FuelTotals.transferred = 0;
            timeRemaining = refuelingDuration;

            if (!Telemagic.isOnFuelApron(vessel)) {
                sourceVessel = vessel.targetObject?.GetVessel();
                if (sourceVessel != null) {
                    sourceVesselFuelTotals = new FuelTotals(sourceVessel);
                    Telemagic.logTM($"will refuel from {sourceVessel.vesselName}");
                }
            }

            // for each resource, compute how much can be transferred.
            foreach (var resource in vesselFuelTotals) {
                var resourceName = resource.Key;
                // how much reserve capacity in the client vessel?
                var capacity = resource.Value.maxAmount - resource.Value.amount;
                var transferAmount = capacity;  // assuming unlimited source
                // if there is a source Target, it will have finite resources.
                if (sourceVessel != null) {
                    var sourceFuelState = sourceVesselFuelTotals?.getResource(resourceName);
                    if (sourceFuelState != null) {
                        if (sourceFuelState.amount < capacity) {
                            transferAmount = sourceFuelState.amount;
                        }
                    }
                }
                resource.Value.targetTransferAmount = transferAmount;
                Telemagic.logTM($"will transfer {resource.Value.targetTransferAmount} of {resourceName}");
            }

            return vessel;
        }

        public void Update() {
            if (vessel == null) {
                vessel = BeginRefueling();
                Telemagic.logTM($"TelemagicFueler starting for {vessel?.vesselName}");
            }

            if (vessel?.parts != null) {
                string resourceName;

                /*  Cycle through all resources and replenish them.

    				Cancel fueling when all full, source is dry or if brakes are released or engines started.
    			*/
                if (!Telemagic.enginesRunning(vessel) && Telemagic.brakesApplied(vessel) && timeRemaining > 0) {
                    var deltaTime = Math.Min(Time.deltaTime, timeRemaining);
                    timeRemaining -= deltaTime;
                    Telemagic.logTM($"----- {deltaTime} with {timeRemaining} remaining -----");
                    int tankNumber = 0;
                    foreach (var part in vessel.parts) {
                        int nres = part.Resources.Count;
                        for (int i = 0; i < nres; ++i) {
                            PartResource resource = part.Resources[i];
                            if (resource.flowState && resource.isVisible) {
                                tankNumber++;
                                resourceName = resource.resourceName;
                                FuelState fuelState = vesselFuelTotals.getResource(resourceName);
                                if (fuelState == null) continue;
                                // apportion the standard flow by available tank capacity as a ratio of total remaining capacity
                                var transferAmount = 0.0;
                                var capacity = resource.maxAmount - resource.amount;
                                if (fuelState.amount < fuelState.maxAmount) {
                                    transferAmount = fuelState.targetTransferAmount
                                        * capacity / (fuelState.maxAmount - fuelState.amount)
                                        * deltaTime / refuelingDuration;
                                }
                                Telemagic.logTM($"{vessel.vesselName} {tankNumber} {resourceName} {resource.amount} {resource.maxAmount} +{transferAmount} target {fuelState.targetTransferAmount}");
                                fuelState.actualTransferAmount += transferAmount;
                                if (transferAmount > 0) {
                                    if (transferAmount > capacity) transferAmount = capacity;
                                    part.TransferResource(resource.info.id, transferAmount);
                                }
                                FuelTotals.transferred += transferAmount;
                            }
                        }
                    }

                    /*  Update total amount transferred to client.
                    */
                    foreach (var total in vesselFuelTotals) {
                        total.Value.amount += total.Value.actualTransferAmount;
                        Telemagic.logTM($"{vessel.vesselName} {total.Value.resourceName} +{total.Value.actualTransferAmount} = {total.Value.amount}");
                        var sourceFuelState = sourceVesselFuelTotals?.getResource(total.Value.resourceName);
                        if (sourceFuelState != null) {
                            sourceFuelState.targetTransferAmount = total.Value.actualTransferAmount;
                            sourceFuelState.actualTransferAmount = 0;
                        }
                        total.Value.actualTransferAmount = 0;
                    }

                    /*  Debit the source vessel, if there is one.
                    */
                    if (sourceVesselFuelTotals != null) {
                        tankNumber = 0;
                        foreach (var part in sourceVessel.parts) {
                            int nparts = part.Resources.Count;
                            for (int partx = 0; partx < nparts; partx++) {
                                PartResource resource = part.Resources[partx];
                                //Telemagic.logTM($"Processing {resource.resourceName} in the source: {sourceVessel?.vesselName}");
                                if (resource.flowState && resource.isVisible) {
                                    tankNumber++;
                                    resourceName = resource.resourceName;
                                    // check that it is a resource that would have been drawn by the client
                                    FuelState fuelState = vesselFuelTotals.getResource(resourceName);
                                    if (fuelState != null && fuelState.targetTransferAmount > 0) {
                                        fuelState = sourceVesselFuelTotals.getResource(resourceName);
                                        // how much?
                                        var transferAmount = 0.0;
                                        if (fuelState.amount > 0) {
                                            transferAmount = resource.amount / fuelState.amount * fuelState.targetTransferAmount;
                                        }
                                        Telemagic.logTM($"{sourceVessel.vesselName} {tankNumber} {resourceName} {resource.amount} {resource.maxAmount} -{transferAmount}");
                                        fuelState.actualTransferAmount += transferAmount;
                                        part.TransferResource(resource.info.id, -transferAmount);
                                        FuelTotals.transferred -= transferAmount;
                                    }
                                }
                            }
                        }
                        // Update total amount withdrawn from source.
                        foreach (var total in sourceVesselFuelTotals) {
                            total.Value.amount -= total.Value.actualTransferAmount;
                            Telemagic.logTM($"{sourceVessel.vesselName} {total.Value.resourceName} -{total.Value.actualTransferAmount} = {total.Value.amount}");
                        }
                    }
                    return;
                }
                Telemagic.message(vessel, $"Refueling {vessel.vesselName} complete.");
            }

            // finished
            Telemagic.logTM($"TelemagicFueler terminating for {vessel?.vesselName}");
            Destroy(this);
        }
    }
}