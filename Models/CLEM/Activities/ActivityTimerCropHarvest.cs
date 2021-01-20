﻿using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace Models.CLEM.Activities
{
    /// <summary>
    /// Activity timer based on crop harvest
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CropActivityTask))]
    [ValidParent(ParentType = typeof(ResourcePricing))]
    [Description("This activity timer is used to determine whether an activity (and all sub activities) will be performed based on the harvest dates of the CropActivityManageProduct above.")]
    [HelpUri(@"Content/Features/Timers/CropHarvest.htm")]
    [Version(1, 0, 2, "Allows timer sequence to be added as child component")]
    [Version(1, 0, 1, "")]
    public class ActivityTimerCropHarvest : CLEMModel, IActivityTimer, IValidatableObject, IActivityPerformedNotifier
    {
        [Link]
        Clock Clock = null;

        /// <summary>
        /// Months before harvest to start performing activities
        /// </summary>
        [Description("Offset from harvest to begin activity (-ve before, 0 harvest, +ve after)")]
        [Required]
        public int OffsetMonthHarvestStart { get; set; }
        /// <summary>
        /// Months before harvest to stop performing activities
        /// </summary>
        [Description("Offset from harvest to end activity (-ve before, 0 harvest, +ve after)")]
        [Required, GreaterThanEqual("OffsetMonthHarvestStart", ErrorMessage = "Offset from harvest to end activity must be greater than or equal to offset to start activity.")]
        public int OffsetMonthHarvestStop { get; set; }
    
        private CropActivityManageProduct ManageProductActivity;
        private List<ActivityTimerSequence> sequenceTimerList;

        /// <summary>
        /// Notify CLEM that this activity was performed.
        /// </summary>
        public event EventHandler ActivityPerformed;

        /// <summary>
        /// Constructor
        /// </summary>
        public ActivityTimerCropHarvest()
        {
            this.SetDefaults();
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            sequenceTimerList = FindAllChildren<ActivityTimerSequence>().ToList<ActivityTimerSequence>();
        }

        /// <summary>
        /// Method to determine whether the activity is due based on harvest details form parent.
        /// </summary>
        /// <returns>Whether the activity is due in the current month</returns>
        public bool ActivityDue
        {
            get
            {
                int[] range = new int[2] { OffsetMonthHarvestStart, OffsetMonthHarvestStop };
                int[] month = new int[2];

                DateTime harvestDate;

                if (ManageProductActivity.PreviousHarvest != null && OffsetMonthHarvestStop > 0)
                {
                    // compare with previous harvest
                    harvestDate = ManageProductActivity.PreviousHarvest.HarvestDate;
                }
                else if (ManageProductActivity.NextHarvest != null && OffsetMonthHarvestStart <= 0)
                {
                    // compare with next harvest 
                    harvestDate = ManageProductActivity.NextHarvest.HarvestDate;
                }
                else
                {
                    // no harvest to compare with
                    return false;
                }

                for (int i = 0; i < 2; i++)
                {
                    DateTime checkDate = harvestDate.AddMonths(range[i]);
                    month[i] = (checkDate.Year * 12 + checkDate.Month);
                }

                int today = Clock.Today.Year * 12 + Clock.Today.Month;
                if (month[0] <= today && month[1] >= today)
                {
                    // report activity performed details.
                    ActivityPerformedEventArgs activitye = new ActivityPerformedEventArgs
                    {
                        Activity = new BlankActivity()
                        {
                            Status = ActivityStatus.Timer,
                            Name = this.Name,
                        }
                    };
                    // check if timer sequence ok
                    if (sequenceTimerList.Count() > 0)
                    {
                        // get month index in sequence
                        int sequenceIndex = today - month[0];
                        foreach (var sequence in sequenceTimerList)
                        {
                            if (!sequence.TimerOK(sequenceIndex))
                            {
                                // report activity performed.
                                activitye.Activity.Status = ActivityStatus.NotNeeded;
                                activitye.Activity.SetGuID(this.UniqueID);
                                this.OnActivityPerformed(activitye);
                                return false;
                            }
                        }
                    }
                    activitye.Activity.SetGuID(this.UniqueID);
                    this.OnActivityPerformed(activitye);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Method to determine whether the activity has past based on current date and harvest details form parent.
        /// </summary>
        /// <returns>Whether the activity is past</returns>
        public bool ActivityPast
        {
            get
            {
                int[] range = new int[2] { OffsetMonthHarvestStart, OffsetMonthHarvestStop };
                int[] month = new int[2];

                DateTime harvestDate;

                if (ManageProductActivity.PreviousHarvest != null & OffsetMonthHarvestStop > 0)
                {
                    // compare with previous harvest
                    harvestDate = ManageProductActivity.PreviousHarvest.HarvestDate;
                }
                else if (ManageProductActivity.NextHarvest != null & OffsetMonthHarvestStart <= 0)
                {
                    // compare with next harvest 
                    harvestDate = ManageProductActivity.NextHarvest.HarvestDate;
                }
                else
                {
                    // no harvest to compare with
                    return false;
                }

                for (int i = 0; i < 2; i++)
                {
                    DateTime checkDate = harvestDate.AddMonths(range[i]);
                    month[i] = (checkDate.Year * 12 + checkDate.Month);
                }
                int today = Clock.Today.Year * 12 + Clock.Today.Month;
                return (month[0] < today && month[1] < today);
            }
        }

        /// <summary>
        /// Method to determine whether the activity is due based on a specified date
        /// </summary>
        /// <returns>Whether the activity is due based on the specified date</returns>
        public bool Check(DateTime dateToCheck)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Activity has occurred 
        /// </summary>
        /// <param name="e"></param>
        public virtual void OnActivityPerformed(EventArgs e)
        {
            ActivityPerformed?.Invoke(this, e);
        }

        #region validation

        /// <summary>
        /// Validate model
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            // check that this activity has a parent of type CropActivityManageProduct

            Model current = this;
            while (current.GetType() != typeof(ZoneCLEM))
            {
                if (current.GetType() == typeof(CropActivityManageProduct))
                {
                    ManageProductActivity = current as CropActivityManageProduct;
                }
                current = current.Parent as Model;
            }

            if (ManageProductActivity == null)
            {
                string[] memberNames = new string[] { "CropActivityManageProduct parent" };
                results.Add(new ValidationResult("This crop timer be below a parent of the type Crop Activity Manage Product", memberNames));
            }

            return results;
        }
        #endregion

        #region descriptive summary

        /// <summary>
        /// Provides the description of the model settings for summary (GetFullSummary)
        /// </summary>
        /// <param name="formatForParentControl">Use full verbose description</param>
        /// <returns></returns>
        public override string ModelSummary(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                if (OffsetMonthHarvestStart + OffsetMonthHarvestStop == 0)
                {
                    htmlWriter.Write("\n<div class=\"filter\">At harvest");
                    htmlWriter.Write("\n</div>");
                }
                else if (OffsetMonthHarvestStop == 0 && OffsetMonthHarvestStart < 0)
                {
                    htmlWriter.Write("\n<div class=\"filter\">");
                    htmlWriter.Write("All <span class=\"setvalueextra\">");
                    htmlWriter.Write(Math.Abs(OffsetMonthHarvestStart).ToString() + "</span> month" + (Math.Abs(OffsetMonthHarvestStart) == 1 ? "" : "s") + " before harvest");
                    htmlWriter.Write("</div>");
                }
                else if (OffsetMonthHarvestStop > 0 && OffsetMonthHarvestStart == 0)
                {
                    htmlWriter.Write("\n<div class=\"filter\">");
                    htmlWriter.Write("All <span class=\"setvalueextra\">");
                    htmlWriter.Write(OffsetMonthHarvestStop.ToString() + "</span> month" + (Math.Abs(OffsetMonthHarvestStop) == 1 ? "" : "s") + " after harvest");
                    htmlWriter.Write("</div>");
                }
                else if (OffsetMonthHarvestStop == OffsetMonthHarvestStart)
                {
                    htmlWriter.Write("\n<div class=\"filter\">");
                    htmlWriter.Write("Perform <span class=\"setvalueextra\">");
                    htmlWriter.Write(Math.Abs(OffsetMonthHarvestStop).ToString() + "</span> month" + (Math.Abs(OffsetMonthHarvestStart) == 1 ? "" : "s") + " " + ((OffsetMonthHarvestStop < 0) ? "before" : "after") + " harvest");
                    htmlWriter.Write("</div>");
                }
                else
                {
                    htmlWriter.Write("\n<div class=\"filter\">");
                    htmlWriter.Write("Start <span class=\"setvalueextra\">");
                    htmlWriter.Write(Math.Abs(OffsetMonthHarvestStart).ToString() + "</span> month" + (Math.Abs(OffsetMonthHarvestStart) == 1 ? "" : "s") + " ");
                    htmlWriter.Write((OffsetMonthHarvestStart > 0) ? "after " : "before ");
                    htmlWriter.Write(" harvest and stop <span class=\"setvalueextra\">");
                    htmlWriter.Write(Math.Abs(OffsetMonthHarvestStop).ToString() + "</span> month" + (Math.Abs(OffsetMonthHarvestStop) == 1 ? "" : "s") + " ");
                    htmlWriter.Write((OffsetMonthHarvestStop > 0) ? "after " : "before ");
                    htmlWriter.Write("</div>");
                }
                if (!this.Enabled)
                {
                    htmlWriter.Write(" - DISABLED!");
                }
                return htmlWriter.ToString(); 
            }
        }

        /// <summary>
        /// Provides the closing html tags for object
        /// </summary>
        /// <returns></returns>
        public override string ModelSummaryClosingTags(bool formatForParentControl)
        {
            return "</div>";
        }

        /// <summary>
        /// Provides the closing html tags for object
        /// </summary>
        /// <returns></returns>
        public override string ModelSummaryOpeningTags(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("<div class=\"filtername\">");
                if (!this.Name.Contains(this.GetType().Name.Split('.').Last()))
                {
                    htmlWriter.Write(this.Name);
                }
                htmlWriter.Write($"</div>");
                htmlWriter.Write("\n<div class=\"filterborder clearfix\" style=\"opacity: " + SummaryOpacity(formatForParentControl).ToString() + "\">");
                return htmlWriter.ToString(); 
            }
        } 
        #endregion
    }
}
