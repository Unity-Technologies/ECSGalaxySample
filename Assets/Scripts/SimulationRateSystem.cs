using Unity.Core;
using Unity.Entities;

namespace Galaxy
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SimulationRateSystem : ISystem
    {
        private bool _hadFirstTimeInit;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationRate>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ref SimulationRate simRate = ref SystemAPI.GetSingletonRW<SimulationRate>().ValueRW;

            if (!_hadFirstTimeInit)
            {
                const string _fixedRateArg = "-fixedRate:";
                
                // Read cmd line args
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg.Contains(_fixedRateArg))
                    {
                        string rate = arg.Substring(_fixedRateArg.Length);
                        if (int.TryParse(rate, out int rateInt))
                        {
                            if (rateInt > 0)
                            {
                                simRate.UseFixedRate = true;
                                simRate.FixedTimeStep = 1f / (float)rateInt;
                            }
                            else
                            {
                                simRate.UseFixedRate = false;
                            }
                            break;
                        }
                    }
                }

                _hadFirstTimeInit = true;
            }

            if (simRate.Update)
            {
                SimulationSystemGroup simulationSystemGroup =
                    state.World.GetExistingSystemManaged<SimulationSystemGroup>();
                
                if (simRate.UseFixedRate)
                {
                    simulationSystemGroup.RateManager = new RateUtils.FixedRateSimpleManager(simRate.FixedTimeStep);
                }
                else
                {
                    simulationSystemGroup.RateManager = null;
                }

                simRate.Update = false;
            }
        }
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(GameInitializeSystem))]
    public partial struct SimulationTimeScaleSystem : ISystem
    {
        private bool _hadFirstTimeInit;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationRate>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ref SimulationRate simRate = ref SystemAPI.GetSingletonRW<SimulationRate>().ValueRW;

            simRate.UnscaledDeltaTime = SystemAPI.Time.DeltaTime;
            
            state.World.SetTime(new TimeData(
                SystemAPI.Time.ElapsedTime,
                SystemAPI.Time.DeltaTime * simRate.TimeScale));
        }
    }
}