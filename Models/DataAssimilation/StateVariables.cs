using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models.Core;
using Models.DataAssimilation.DataType;
using System.Threading;
using Models.Soils;
//using System.Data.SQLite;
using Models.PMF;
using Models.PMF.Organs;
using APSIM.Shared.Utilities;
using System.Data.SQLite;
using Models.WaterModel;
using Models.Soils.SoilTemp;
using Models.PMF.Struct;

namespace Models.DataAssimilation
{
    /// <summary>
    /// A class for state variales.
    /// This class links all the model states and store them in StateTable class.
    /// Get and set states in modules.
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.ProfileView")]
    [PresenterName("UserInterface.Presenters.ProfilePresenter")]
    [ValidParent(ParentType = typeof(Simulation))]
    [ValidParent(ParentType = typeof(IDataAssimilation))]
    [ValidParent(ParentType = typeof(Control))]

    public class StateVariables : Model
    {
        #region ******* Links. *******

        [Link]
        Clock Clock = null;

        [Link]
        Control Control = null;

        [Link(IsOptional = true)]
        Leaf Leaf = null;

        [Link(IsOptional = true)]
        WaterBalance SoilWater = null;

        //[Link(IsOptional = true)]
        //SoilTemperature SoilTemperature = null;

        //[Link(IsOptional = true)]
        //SoilNitrogenNO3 SoilNO3 = null;

        //[Link(IsOptional = true)]
        //SoilNitrogenNH4 SoilNH4 = null;

        #endregion

        #region ******* Public field. *******

        /// <summary>State variables involved in the State vector.</summary>
        [Description("State variables involved in the State vectors")]
        public string[] StateNames { get; set; }
        /// <summary>Extra output apart from StateNames.</summary>
        [Description("Data Assimiation Output")]
        public string[] OutputExtra { get; set; }
        /// <summary>ModelError</summary>
        [Description("Model physics error")]
        [Display(Format = "N3")]
        public double[] ModelError { get; set; }

        /// <summary>ModelErrorOption</summary>
        [Description("Model physics error options: 0-Not to perturb; 1-Addtive; 2-Multiplicative.")]
        public int[] ModelErrorOption { get; set; }

        /// <summary>The numbler of state variables involved in the State vector.</summary>
        [XmlIgnore]
        public int Count { get { return StateNames.Count(); } }

        #endregion

        #region ******* Private field. *******

        //Store all the states here.
        private static List<StateTable> StatesData = new List<StateTable>();
        private static List<StateTable> OutputData = new List<StateTable>();

        private FolderInfo Info = new FolderInfo();

        #endregion

        #region ******* EventHandlers. *******

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [EventSubscribe("PrepareAssimilation")]
        private void OnPrepareAssimilation(object sender, EventArgs e)
        {
            if (Control.DAOption != null && Thread.CurrentThread.Name == "Truth")
            {
                NewStatesData();
                if (OutputExtra != null)
                    NewOutputData();
            }
        }

        [EventSubscribe("NewDay")]
        private void OnNewDay(object sender, EventArgs e)
        {
            if (Control.DAOption != null && Thread.CurrentThread.Name == "Truth")
            {
                foreach (StateTable table in StatesData)
                {
                    table.NewRow(Clock.ID, Clock.Today);
                }
                foreach (StateTable table in OutputData)
                {
                    table.NewRow(Clock.ID, Clock.Today);
                }
            }
        }

        /// <summary> Write prior results. </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [EventSubscribe("WritePriorResult")]
        private void OnWritePriorResult(object sender, EventArgs e)
        {
            if (Control.DAOption != null)
            {
                Insert();
                InsertOutput("Prior");
            }
        }

        /// <summary> Write Posterior results. </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [EventSubscribe("WritePosteriorResult")]
        private void OnWritePosteriorResult(object sender, EventArgs e)
        {
            if (Control.DAOption != null)
            {
                Update();
                InsertOutput("Posterior");
            }
        }

        /// <summary> Write Posterior results. </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [EventSubscribe("WriteSQLite")]
        private void OnWriteSQLite(object sender, EventArgs e)
        {
            if (Control.DAOption != null)
            {
                WriteSQLite();
                WriteSQLiteOutput();
            }
        }
        #endregion

        #region ******* Methods. *******

        #region Link state variables to all modules.
        /// <summary>
        /// Get the value of state variables based on their names.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public double GetFromApsim(string tableName)
        {
            double value;
            switch (tableName)
            {
                // Plant.
                case "LAI":
                    value = Leaf.LAI;
                    break;
                case "Height":
                    value = Leaf.Height;
                    break;
                case "Width":
                    value = Leaf.Width;
                    break;
                case "CoverGreen":
                    value = Leaf.CoverGreen;
                    break;
                case "FRGR":
                    value = Leaf.FRGR;
                    break;
                case "PET":
                    value = Leaf.PotentialEP;
                    break;
                case "WaterDemand":
                    value = Leaf.WaterDemand;
                    break;

                //Soil.
                case "SW1":
                    value = SoilWater.SW[0];
                    break;
                case "SW2":
                    value = SoilWater.SW[1];
                    break;
                case "SW3":
                    value = SoilWater.SW[2];
                    break;
                case "SW4":
                    value = SoilWater.SW[3];
                    break;
                case "SW5":
                    value = SoilWater.SW[4];
                    break;
                case "SW6":
                    value = SoilWater.SW[5];
                    break;
                case "SW7":
                    value = SoilWater.SW[6];
                    break;

                default:
                    {
                        value = -99;
                        break;
                    }
            }
            return value;
        }

        /// <summary>
        /// Get the value of state variables based on their names.
        /// Tips: APSIM states can be updated through Links!
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="value"></param>
        public void ReturnToApsim(string tableName, double value)
        {
            double[] newSW = SoilWater.SW;
            //double[] newSNO3 = SoilNO3.kgha;
            //double[] newSNH4 = SoilNH4.kgha;

            switch (tableName)
            {
                // Plant.
                case "LAI":
                    Leaf.LAI = Constrain(value, 0, value + 1);
                    break;

                // Soil.
                case "SW1":
                    newSW[0] = Constrain(value, 0, 1);
                    break;
                case "SW2":
                    newSW[1] = Constrain(value, 0, 1);
                    break;
                case "SW3":
                    newSW[2] = Constrain(value, 0, 1);
                    break;
                case "SW4":
                    newSW[3] = Constrain(value, 0, 1);
                    break;
                case "SW5":
                    newSW[4] = Constrain(value, 0, 1);
                    break;
                case "SW6":
                    newSW[5] = Constrain(value, 0, 1);
                    break;
                case "SW7":
                    newSW[6] = Constrain(value, 0, 1);
                    break;

                default:
                    Console.WriteLine("Warning: Wrong state variable name!");
                    break;
            }
            SoilWater.SW = newSW;
            //SoilNO3.kgha = newSNO3;
            //SoilNH4.kgha = newSNH4;

        }



        #endregion
        /// <summary>
        /// Initialize the StateTables.
        /// Call on the first of data assimilation day.
        /// </summary>
        public void NewStatesData()
        {
            foreach (string stateName in StateNames)
            {
                StateTable temp = new StateTable(stateName, Control.EnsembleSize);
                StatesData.Add(temp);
            }
        }

        /// <summary>
        /// Initialize the output data (in addition to states data).
        /// </summary>
        public void NewOutputData()
        {
            foreach (string output in OutputExtra)
            {
                StateTable temp = new StateTable(output, Control.EnsembleSize);
                OutputData.Add(temp);
            }
        }

        #region ******** Data exchange between APSIM and StateTable. ********
        /// <summary>
        /// Insert all prior state variables of a single ensemble to StateTable.
        /// </summary>
        public void Insert()
        {
            double value;
            string columnName = Thread.CurrentThread.Name;
            if (columnName.Contains("Ensemble") || columnName == "OpenLoop")
            {
                columnName = "Prior" + columnName;
            }

            foreach (StateTable table in StatesData)
            {
                value = GetFromApsim(table.TableName);
                table.InsertSingle(value, Clock.ID, columnName);
            }
        }

        /// <summary>
        /// For Extra Output.
        /// </summary>
        public void InsertOutput(string keyword)
        {
            double value;
            string columnName = Thread.CurrentThread.Name;
            if (columnName.Contains("Ensemble") || columnName == "OpenLoop")
            {
                columnName = keyword + columnName;
            }

            foreach (StateTable table in OutputData)
            {
                value = GetFromApsim(table.TableName);
                table.InsertSingle(value, Clock.ID, columnName);
            }
        }
        /// <summary>
        /// Update all posterior state variables of a single ensemble to Apsim.
        /// </summary>
        public void Update()
        {
            double value;
            string columnName = Thread.CurrentThread.Name;
            if (columnName.Contains("Ensemble"))
            {
                columnName = "Posterior" + columnName;

                foreach (StateTable table in StatesData)
                {
                    value = table.GetSingle(Clock.ID, columnName);
                    ReturnToApsim(table.TableName, value);
                }
            }
        }

        /// <summary>
        /// Constrain values in a range.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="lower"></param>
        /// <param name="upper"></param>
        /// <returns></returns>
        public double Constrain(double value, double lower, double upper)
        {
            return Math.Min(Math.Max(lower, value), upper);
        }

        #endregion

        #region ******** Data exchange between StateTable and IDataAssiimlation. ********
        //Via StatesOfTheDay.

        /// <summary>
        /// From StateTable to StateOfTheDay.
        /// Called by IDataAssimilaiton At the begining of DoDataAssimilation.
        /// </summary>
        /// <returns></returns>
        public StatesOfTheDay GetPrior()
        {
            StatesOfTheDay States = new StatesOfTheDay();
            double[] ensemble;
            double openLoop;
            foreach (StateTable table in StatesData)
            {
                ensemble = table.ReadPrior(Clock.ID, Control.EnsembleSize);
                States.Prior.Add(ensemble);
                openLoop = table.ReadOpenLoop(Clock.ID);
                States.PriorOL.Add(openLoop);
            }
            return States;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public StatesOfTheDay GetOutputPrior()
        {
            StatesOfTheDay Output = new StatesOfTheDay();
            double[] ensemble;
            double openLoop;
            foreach (StateTable table in OutputData)
            {
                ensemble = table.ReadPrior(Clock.ID, Control.EnsembleSize);
                Output.Prior.Add(ensemble);
                openLoop = table.ReadOpenLoop(Clock.ID);
                Output.PriorOL.Add(openLoop);
            }
            return Output;
        }

        /// <summary>
        /// Call by StateVariables At the end of DoDataAssimilation.
        /// </summary>
        /// <returns></returns>
        public void ReturnPosterior(StatesOfTheDay states)
        {
            int tableIndex = 0;
            foreach (StateTable table in StatesData)
            {
                table.InsertPosterior(Clock.ID, states, Control.DAOption, tableIndex, Control.EnsembleSize);
                tableIndex++;
            }
        }

        /// <summary>
        /// Call by StateVariables At the end of DoDataAssimilation.
        /// For extra output.
        /// </summary>
        /// <returns></returns>
        public void ReturnOutputPosterior(StatesOfTheDay output)
        {
            int tableIndex = 0;
            foreach (StateTable table in OutputData)
            {
                table.InsertOutputPosterior(Clock.ID, output, Control.DAOption, tableIndex, Control.EnsembleSize);
                tableIndex++;
            }
        }

        #endregion

        #region ******** Write results to SQLite. ********

        /// <summary>
        /// 
        /// </summary>
        public void WriteSQLite()
        {
            Console.WriteLine("Creating SQLite...");
            SQLiteConnection.CreateFile(Info.SQLite);
            SQLiteConnection sqlCon = new SQLiteConnection("Data Source=" + Info.SQLite);
            sqlCon.Open();
            double span = Clock.Today.Subtract(Clock.StartDate).TotalDays + 1;
            int length = Convert.ToInt16(span);
            int temp = -1;

            // This part was in Loc 1 inside the loop.
            string sqlStr;
            SQLiteTransaction sqlTran = sqlCon.BeginTransaction();
            SQLiteCommand command = sqlCon.CreateCommand();
            command.Transaction = sqlTran;
            // End

            foreach (StateTable table in StatesData)
            {
                ++temp;
                if (temp < 100)   //Export the first 100 pages.
                { }
                else
                {
                    continue;
                }

                Console.Write("\tWriting to Table [{0}]...", table.TableName);
                CreateSQLiteTable(Control.EnsembleSize, table.TableName, sqlCon, Info);
                InsertRows(table, sqlCon, length);

                List<string> ColumnNames = new List<string>();
                ColumnNames.Add("Truth");
                ColumnNames.Add("PriorOpenLoop");
                for (int i = 0; i < Control.EnsembleSize; i++)
                    ColumnNames.Add("PriorEnsemble" + i.ToString());
                ColumnNames.Add("PriorMean");

                ColumnNames.Add("PosteriorOpenLoop");
                for (int i = 0; i < Control.EnsembleSize; i++)
                    ColumnNames.Add("PosteriorEnsemble" + i.ToString());
                ColumnNames.Add("PosteriorMean");

                ColumnNames.Add("Obs");
                for (int i = 0; i < Control.EnsembleSize; i++)
                    ColumnNames.Add("ObsEnsemble" + i.ToString());

                // Loc 1

                foreach (string colName in ColumnNames)
                {
                    for (int i = 0; i < length; i++)
                    {
                        sqlStr = "UPDATE " + table.TableName + " SET ";
                        sqlStr += colName + "='" + table.Table.Rows[i][colName] + "' WHERE ID= '" + i + "'";
                        command.CommandText = sqlStr;
                        command.ExecuteNonQuery();
                    }
                }
            }

            // This part was in Loc 2 inside the loop.
            sqlTran.Commit();
            Console.WriteLine("Done!");
            // end

            sqlCon.Close();
            Console.WriteLine("SQLite has been created!");
        }


        /// <summary>
        /// For extra output.
        /// </summary>
        public void WriteSQLiteOutput()
        {
            Console.WriteLine("Creating SQLiteExtra for extra output...");
            SQLiteConnection.CreateFile(Info.SQLiteOutput);
            SQLiteConnection sqlCon = new SQLiteConnection("Data Source=" + Info.SQLiteOutput);
            sqlCon.Open();
            double span = Clock.Today.Subtract(Clock.StartDate).TotalDays + 1;
            int length = Convert.ToInt16(span);
            int temp = -1;

            // This part was in Loc 1 inside the loop.
            string sqlStr;
            SQLiteTransaction sqlTran = sqlCon.BeginTransaction();
            SQLiteCommand command = sqlCon.CreateCommand();
            command.Transaction = sqlTran;
            // End

            foreach (StateTable table in OutputData)
            {
                ++temp;

                Console.Write("\tWriting to Table [{0}]...", table.TableName);
                CreateSQLiteTable(Control.EnsembleSize, table.TableName, sqlCon, Info);
                InsertRows(table, sqlCon, length);

                List<string> ColumnNames = new List<string>();
                ColumnNames.Add("Truth");
                ColumnNames.Add("PriorOpenLoop");
                for (int i = 0; i < Control.EnsembleSize; i++)
                    ColumnNames.Add("PriorEnsemble" + i.ToString());
                ColumnNames.Add("PriorMean");

                ColumnNames.Add("PosteriorOpenLoop");
                for (int i = 0; i < Control.EnsembleSize; i++)
                    ColumnNames.Add("PosteriorEnsemble" + i.ToString());
                ColumnNames.Add("PosteriorMean");

                ColumnNames.Add("Obs");
                for (int i = 0; i < Control.EnsembleSize; i++)
                    ColumnNames.Add("ObsEnsemble" + i.ToString());

                // Loc 1.

                foreach (string colName in ColumnNames)
                {
                    for (int i = 0; i < length; i++)
                    {
                        sqlStr = "UPDATE " + table.TableName + " SET ";
                        sqlStr += colName + "='" + table.Table.Rows[i][colName] + "' WHERE ID= '" + i + "'";
                        command.CommandText = sqlStr;
                        command.ExecuteNonQuery();
                    }
                }
                // Loc 2.
            }

            // This part was in Loc 2 inside the loop.
            sqlTran.Commit();
            Console.WriteLine("Done!");
            // end

            sqlCon.Close();
            Console.WriteLine("SQLiteExtra has been created!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ensembleSize"></param>
        /// <param name="tableName"></param>
        /// <param name="sqlCon"></param>
        /// <param name="info"></param>
        public static void CreateSQLiteTable(int ensembleSize, string tableName, SQLiteConnection sqlCon, FolderInfo info)
        {
            string sqlStr = "CREATE TABLE " + tableName + " (ID int PRIMARY KEY, Date string, DOY int, Truth double, ";

            sqlStr += "PriorOpenLoop double, ";
            for (int i = 0; i < ensembleSize; i++)
            {
                sqlStr += "PriorEnsemble" + i + " double, ";
            }
            sqlStr += "PriorMean double, ";
            sqlStr += "PosteriorOpenLoop double, ";
            for (int i = 0; i < ensembleSize; i++)
            {
                sqlStr += "PosteriorEnsemble" + i + " double, ";
            }
            sqlStr += "PosteriorMean double, ";

            sqlStr += "Obs double";
            for (int i = 0; i < ensembleSize; i++)
            {
                sqlStr += ", ObsEnsemble" + i + " double";
            }
            sqlStr += ")";

            SQLiteCommand command = new SQLiteCommand(sqlStr, sqlCon);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <param name="sqlCon"></param>
        /// <param name="length"></param>
        public void InsertRows(StateTable table, SQLiteConnection sqlCon, int length)
        {
            string sqlStr;
            SQLiteCommand command;
            for (int j = 0; j < length; j++)
            {
                sqlStr = "INSERT INTO " + table.TableName;
                sqlStr += " (ID,Date, DOY) VALUES (";
                sqlStr += "'" + table.Table.Rows[j]["ID"] + "', ";
                sqlStr += "'" + table.Table.Rows[j]["Date"].ToString() + "', ";
                sqlStr += "'" + table.Table.Rows[j]["DOY"] + "')";

                command = new SQLiteCommand(sqlStr, sqlCon);
                command.ExecuteNonQuery();
            }
        }

        #endregion

        #endregion

    }
}
