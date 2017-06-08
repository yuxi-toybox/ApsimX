﻿using Models.Core;
using Models.WholeFarm.Groupings;
using Models.WholeFarm.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Models.WholeFarm.Activities
{
	/// <summary>Activity to remove the labour used for graowing a crop</summary>
	[Serializable]
	[ViewName("UserInterface.Views.GridView")]
	[PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(IATGrowCrop))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    public class IATGrowCropLabour : WFActivityBase
	{
        /// <summary>
        /// Get the Clock.
        /// </summary>
        [XmlIgnore]
        [Link]
        Clock Clock = null;

        [XmlIgnore]
        [Link]
        ISummary Summary = null;


        /// <summary>
        /// Months before harvest to sow crop
        /// </summary>
        [Description("Months before harvest to apply cost")]
        public int MthsBeforeHarvest { get; set; }


        /// <summary>
        /// Date to apply the cost on.
        /// Has to be stored as a global variable because of a race condition that occurs if user sets  MthsBeforeHarvest=0
        /// Then because ParentGrowCrop and children are all executing on the harvest month and 
        /// the ParentGrowCrop executes first and it removes the harvest from the harvest list, 
        /// then the chidren such as these never get the Clock.Today == harvest date (aka costdate).
        /// So instead we store the next harvest date (aka costdate) in this variable and don't update its value
        /// until after we have done the Clock.Today == harvest date (aka costdate) comparison.
        /// </summary>
        private DateTime costDate;


        /// <summary>
        /// Parent of this model
        /// </summary>
        private IATGrowCrop ParentGrowCrop;

        /// <summary>
        /// Labour settings
        /// </summary>
        private List<LabourFilterGroupSpecified> labour { get; set; }




        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            bool foundGrowCrop = FindParentGrowCrop();
            if (!foundGrowCrop)
            {
                Summary.WriteWarning(this, String.Format("Unable to find a parent IATGrowCrop anywhere above ({0}).", this.Name));
                throw new ApsimXException(this, String.Format("Unable to find a parent IATGrowCrop anywhere above ({0}).", this.Name));
            }

            // get labour specifications
            labour = this.Children.Where(a => a.GetType() == typeof(LabourFilterGroupSpecified)).Cast<LabourFilterGroupSpecified>().ToList();
			if (labour == null) labour = new List<LabourFilterGroupSpecified>();


            costDate = CostDateFromHarvestDate();
        }



        /// <summary>
        /// Find a parent of type IATGrowCrop somewhere above this model in the simulation tree.
        /// </summary>
        /// <returns>true or false whether this found it</returns>
        private bool FindParentGrowCrop()
        {

            IModel temp = this.Parent;

            //stop when you hit the top level Activity folder if you have not found it yet.
            while ((temp is ActivitiesHolder) == false)
            {
                //if you have found it.
                if (temp is IATGrowCrop)
                {
                    ParentGrowCrop = (IATGrowCrop)temp;  //set the global variable to it.
                    return true;
                }
                //else go up one more folder level
                temp = temp.Parent;
            }

            return false;
        }


        /// <summary>
        /// Get the cost date from the harvest date.
        /// This will happen every month in case the harvest has occured and there is a new harvest date.
        /// </summary>
        /// <returns></returns>
        private DateTime CostDateFromHarvestDate()
        {
            DateTime nextdate;
            CropDataType nextharvest = ParentGrowCrop.HarvestData.FirstOrDefault();
            if (nextharvest != null)
            {
                nextdate = nextharvest.HarvestDate;
                return nextdate.AddMonths(-1 * MthsBeforeHarvest);
            }
            else
            {
                return new DateTime();
            }
        }


        /// <summary>
        /// Method to determine resources required for this activity in the current month
        /// </summary>
        /// <returns>List of required resource requests</returns>
        public override List<ResourceRequest> GetResourcesNeededForActivity()
        {
			ResourceRequestList = null;


            if ((costDate.Year == Clock.Today.Year) && (costDate.Month == Clock.Today.Month))
            {
                string cropName = ParentGrowCrop.FeedTypeName;

                // for each labour item specified
                foreach (var item in labour)
                {
                    double daysNeeded = 0;
                    switch (item.UnitType)
                    {
                        case LabourUnitType.Fixed:
                            daysNeeded = item.LabourPerUnit;
                            break;
                        default:
                            throw new Exception(String.Format("LabourUnitType {0} is not supported for {1} in {2}", item.UnitType, item.Name, this.Name));
                    }
                    if (daysNeeded > 0)
                    {
                        if (ResourceRequestList == null) ResourceRequestList = new List<ResourceRequest>();
                        ResourceRequestList.Add(new ResourceRequest()
                        {
                            AllowTransmutation = false,
                            Required = daysNeeded,
                            ResourceType = typeof(Labour),
                            ResourceTypeName = "",
                            ActivityModel = this,
                            Reason = "Crop labour (fixed) - " + cropName,
                            FilterDetails = new List<object>() { item }
                        }
                        );
                    }
                }
            }

            costDate = CostDateFromHarvestDate();

            return ResourceRequestList;
		}

        /// <summary>
        /// Method used to perform activity if it can occur as soon as resources are available.
        /// </summary>
        public override void DoActivity()
        {
            return;
        }

        /// <summary>
        /// Method to determine resources required for initialisation of this activity
        /// </summary>
        /// <returns></returns>
        public override List<ResourceRequest> GetResourcesNeededForinitialisation()
        {
            return null;
        }

        /// <summary>
        /// Method used to perform initialisation of this activity.
        /// This will honour ReportErrorAndStop action but will otherwise be preformed regardless of resources available
        /// It is the responsibility of this activity to determine resources provided.
        /// </summary>
        public override void DoInitialisation()
        {
            return;
        }

        /// <summary>
        /// Resource shortfall event handler
        /// </summary>
        public override event EventHandler ResourceShortfallOccurred;

		/// <summary>
		/// Shortfall occurred 
		/// </summary>
		/// <param name="e"></param>
		protected override void OnShortfallOccurred(EventArgs e)
		{
			if (ResourceShortfallOccurred != null)
				ResourceShortfallOccurred(this, e);
		}

	}
}
