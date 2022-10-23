﻿// COPYRIGHT 2022 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Orts.Parsers.Msts;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using ORTS.Scripting.Api;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
    public class VoltageSelectorPosition
    {
        public string Name;
        public int VoltageV;

        public VoltageSelectorPosition(string name, int voltageV)
        {
            Name = name;
            VoltageV = voltageV;
        }
    }

    public class ScriptedVoltageSelector : ISubSystem<ScriptedVoltageSelector>
    {
        public ScriptedLocomotivePowerSupply PowerSupply;
        public MSTSLocomotive Locomotive => PowerSupply.Locomotive;
        public Simulator Simulator => Locomotive.Simulator;

        /// <summary>
        /// List of all selector positions
        /// </summary>
        public List<VoltageSelectorPosition> Positions = new List<VoltageSelectorPosition>();
        /// <summary>
        /// Current position of the selector
        /// </summary>
        public VoltageSelectorPosition Position;
        public int PositionId => Positions.IndexOf(Position);

        /// <summary>
        /// Relative path to the script from the script directory of the locomotive
        /// </summary>
        public string ScriptPath;

        public VoltageSelector Script;

        public ScriptedVoltageSelector(ScriptedLocomotivePowerSupply powerSupply)
        {
            PowerSupply = powerSupply;
        }

        public void Parse(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new[]
            {
                new STFReader.TokenProcessor("script", () => { ScriptPath = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("selectorpositions", () =>
                {
                    stf.MustMatch("(");
                    stf.ParseBlock(new []
                    {
                        new STFReader.TokenProcessor("selectorposition", () =>
                        {
                            string name = null;
                            int voltageV = -1;

                            stf.MustMatch("(");
                            stf.ParseBlock(new[]
                            {
                                new STFReader.TokenProcessor("name", () => name = stf.ReadStringBlock(null)),
                                new STFReader.TokenProcessor("voltage", () => voltageV = Convert.ToInt32(stf.ReadFloatBlock(STFReader.UNITS.Voltage, -1))),
                            });

                            if (name != null && voltageV > 0 && !Positions.Exists(x => x.Name == name))
                            {
                                Positions.Add(new VoltageSelectorPosition(name, voltageV));
                            }
                            else
                            {
                                Trace.TraceWarning($"Ignored voltage selector position with name {name} and voltage {voltageV}");
                            }
                        }),
                    });
                }),
            });
        }

        public void Copy(ScriptedVoltageSelector other)
        {
            foreach (VoltageSelectorPosition position in other.Positions)
            {
                Positions.Add(position);
            }

            ScriptPath = other.ScriptPath;
        }

        public void Initialize()
        {
            Position =
                Positions.FirstOrDefault(x =>
                    x.VoltageV == PowerSupply.Locomotive.Train.Simulator.TRK.Tr_RouteFile.MaxLineVoltage) ??
                Positions.FirstOrDefault();

            if (Script == null && ScriptPath != null)
            {
                string[] pathArray = { Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script") };
                Script = Simulator.ScriptManager.Load(pathArray, ScriptPath) as VoltageSelector;
            }

            Script?.AttachToHost(this);
            Script?.Initialize();
        }

        public void InitializeMoving()
        {
            Script?.InitializeMoving();
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write(Position.Name);
        }

        public void Restore(BinaryReader inf)
        {
            string name = inf.ReadString();
            Position = Positions.FirstOrDefault(x => x.Name == name) ?? Positions.FirstOrDefault();
        }

        public void Update(float elapsedClockSeconds)
        {
            Script?.Update(elapsedClockSeconds);
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            Script?.HandleEvent(evt);
        }

        public void HandleEvent(PowerSupplyEvent evt, int id)
        {
            Script?.HandleEvent(evt, id);
        }
    }
}