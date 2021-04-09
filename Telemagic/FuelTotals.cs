using System.Collections.Generic;

namespace Telemagic {

    public class FuelState {

        public string resourceName;
        public double amount;
        public double maxAmount;
        public double targetTransferAmount;

        // for debugging
        public double actualTransferAmount;
    }

    public class FuelTotals : Dictionary<string, FuelState> {

        public static double transferred;

        public FuelTotals(Vessel vessel) {
            if (vessel?.parts != null) {
                foreach (var part in vessel.parts) {
                    int nres = part.Resources.Count;
                    for (int i = 0; i < nres; ++i) {
                        PartResource resource = part.Resources[i];
                        if (resource.flowState && resource.isVisible) {
                            var resourceName = resource.resourceName;
                            FuelState fuelState;
                            if (!this.ContainsKey(resourceName)) {
                                fuelState = new FuelState();
                                fuelState.resourceName = resourceName;
                                this[resourceName] = fuelState;
                            } else fuelState = this[resourceName];
                            fuelState.amount += resource.amount;
                            fuelState.maxAmount += resource.maxAmount;
                        }
                    }
                }
                foreach (var total in this) {
                    Telemagic.logTM($"{vessel.vesselName} {total.Value.resourceName} {total.Value.amount} {total.Value.maxAmount}");
                }
            }
        }

        public FuelState getResource(string resourceName) {
            if (!this.ContainsKey(resourceName)) return null;
            return this[resourceName];
        }
    }
}
