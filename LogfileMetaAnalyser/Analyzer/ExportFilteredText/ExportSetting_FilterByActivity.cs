﻿using System;
using System.Collections.Generic;
using System.Linq; 
using Newtonsoft.Json;

using LogfileMetaAnalyser.Datastore;
using LogfileMetaAnalyser.Helpers;

namespace LogfileMetaAnalyser 
{
    public class ExportSetting_FilterByActivity : IExportSetting
    {
        private DatastoreStructure dsref;
        private static string[] jsonExportTakeAttributeLst = new string[] { "isfilterEnabled_ProjectionActivity",
                                                                            "isfilterEnabled_ProjectionActivity_Projections",
                                                                            "isfilterEnabled_ProjectionActivity_Projections_AdHoc", "isfilterEnabled_ProjectionActivity_Projections_Sync",
                                                                            "isfilterEnabled_JobServiceActivity",
                                                                            "isfilterEnabled_JobServiceActivity_ByComponent", "isfilterEnabled_JobServiceActivity_ByQueue"
                                                                            };


        //Profile relevant
        public bool isfilterEnabled_ProjectionActivity = false;
        public bool isfilterEnabled_ProjectionActivity_Projections = false;
        public bool isfilterEnabled_ProjectionActivity_Projections_AdHoc = false;
        public bool isfilterEnabled_ProjectionActivity_Projections_Sync = false;

        public bool isfilterEnabled_JobServiceActivity = false;
        public bool isfilterEnabled_JobServiceActivity_ByComponent = false;
        public bool isfilterEnabled_JobServiceActivity_ByQueue = false;
 
        //Non-Profile relevant
        public List<string> filterProjectionActivity_Projections_AdHocLst = new List<string>();  //list of uuids
        public List<string> filterProjectionActivity_Projections_SyncLst = new List<string>();  //list of uuids
        public List<string> filterJobServiceActivity_ByComponentLst = new List<string>();  //list of fulltaskname 
        public List<string> filterJobServiceActivity_ByQueueLst = new List<string>();  //list of queuename

        private bool isfilterByActivity_SpidFilter_passUnseen = false;  //do not check, pass the filter
        private HashSet<string> filterByActivitySpidHashLst = new HashSet<string>();    //https://stackoverflow.com/questions/2728500/hashsett-versus-dictionaryk-v-w-r-t-searching-time-to-find-if-an-item-exist




        public ExportSetting_FilterByActivity(DatastoreStructure datastore)
        {
            dsref = datastore; 
        }

        public DatastoreStructure datastore
        {
            set { dsref = value; }
        }


        public void Prepare()
        {
            IEnumerable<string> spids;

            //-------------------------------
            //reset everything
            //-------------------------------
            filterByActivitySpidHashLst.Clear();
            isfilterByActivity_SpidFilter_passUnseen = false;  //do not check, pass the filter

 
            //---------------------------------------------------------------------
            //Evaluating which filter is to use and therefor needs to be populated
            //---------------------------------------------------------------------
            
            if (isfilterEnabled_ProjectionActivity_Projections_AdHoc || isfilterEnabled_ProjectionActivity_Projections_Sync)
            {
                isfilterEnabled_ProjectionActivity = true;
                isfilterEnabled_ProjectionActivity_Projections = true;
            }

            if (isfilterEnabled_ProjectionActivity_Projections && !isfilterEnabled_ProjectionActivity)
                isfilterEnabled_ProjectionActivity = true;

            if ((isfilterEnabled_JobServiceActivity_ByComponent || isfilterEnabled_JobServiceActivity_ByQueue) && !isfilterEnabled_JobServiceActivity)
                isfilterEnabled_JobServiceActivity = true;

            //this should have been done by GUI, when a parent node is disabled, all child nodes should be disabled as well, but we can do it right here again
            if (!isfilterEnabled_ProjectionActivity_Projections_AdHoc)
                filterProjectionActivity_Projections_AdHocLst.Clear();

            if (!isfilterEnabled_ProjectionActivity_Projections_Sync)
                filterProjectionActivity_Projections_SyncLst.Clear();

            if (!isfilterEnabled_JobServiceActivity_ByComponent)
                filterJobServiceActivity_ByComponentLst.Clear();

            if (!isfilterEnabled_JobServiceActivity_ByQueue)
                filterJobServiceActivity_ByQueueLst.Clear();


            //its more: when I select a specific component (or all), I want to see all jobs of this component + their results and warnings
            //if I select a specific queue, I want to see all activities of this queue, icl. job start and finish, optional all request messages 

            //first check for a) enabled but nothing is choosen and b) enabled but all is choosen -> filter none 


            //a): if nothing was enabled in the filter GUI, does it now mean: get it all or block it all? Basically: Get it all ;)
            if (!isfilterEnabled_JobServiceActivity && !isfilterEnabled_ProjectionActivity)
            {
                isfilterByActivity_SpidFilter_passUnseen = true;
                return;
            }


            //b): filters are partially set

            bool takeAllAdHoc = isfilterEnabled_ProjectionActivity_Projections_AdHoc && (
                                    !filterProjectionActivity_Projections_AdHocLst.Any()
                                     || 
                                    filterProjectionActivity_Projections_AdHocLst.Count == dsref.projectionActivity.NumberOfAdHocProjections);

            bool takeAllSync = isfilterEnabled_ProjectionActivity_Projections_Sync && (
                                    !filterProjectionActivity_Projections_SyncLst.Any()
                                     ||
                                    filterProjectionActivity_Projections_SyncLst.Count == dsref.projectionActivity.NumberOfSyncProjections);

            //here the option isfilterEnabled_JobServiceActivity_ByX has another meaning: when enabled, respect the filterJobServiceActivity_ByXLst, otherwise take all
            bool takeAllJsJobByComponent = isfilterEnabled_JobServiceActivity_ByComponent && (
                                    !filterJobServiceActivity_ByComponentLst.Any()
                                     ||
                                    filterJobServiceActivity_ByComponentLst.Count == dsref.jobserviceActivities.distinctTaskfull.Count);

            bool takeAllJsJobByQueue = isfilterEnabled_JobServiceActivity_ByQueue && (
                                    !filterJobServiceActivity_ByQueueLst.Any()
                                     ||
                                    filterJobServiceActivity_ByQueueLst.Count == dsref.jobserviceActivities.distinctQueuename.Count);

            bool takeAllJsJob = takeAllJsJobByComponent || takeAllJsJobByQueue;  //if one filter says TakeAllByX a filter ByY is meaningless


            isfilterByActivity_SpidFilter_passUnseen = //all options are active AND everything is included in the scope
                    takeAllAdHoc && // AdHoc filter option is enabled and all AdHoc jobs should be included
                    takeAllSync && // Sync filter option is enabled and all Sync jobs should be included
                    takeAllJsJob; //include all Jobservice jobs regardless their Component or Queue

 
            if (isfilterByActivity_SpidFilter_passUnseen)
                return;


            if (isfilterEnabled_ProjectionActivity_Projections)
            {
                // -> collect all SPIDs (not uuid) and put it in a hash table
                //ds.projectionActivity.projections[0].uuid
                //ds.projectionActivity.projections[0].systemConnectors[0].uuid
                //ds.projectionActivity.projections[0].projectionSteps[0].uuid
                //ds.projectionActivity.projections[0].specificSqlInformation.sqlSessions[0].uuid
                //ds.generalSqlInformation.sqlSessions[0].uuid


                //we should filter by AdHoc jobs, but no single job was specified: include ALL
                if (takeAllAdHoc)
                {
                    spids = dsref.projectionActivity.GetLoggerIdsByUuids(new string[] { "*" }, true, ProjectionType.AdHocProvision);
                    spids.Union(dsref.generalSqlInformation.GetLoggerIdsByUuids(new string[] { "*" }));
                }
                else
                {
                    spids = dsref.projectionActivity.GetLoggerIdsByUuids(filterProjectionActivity_Projections_AdHocLst.ToArray(), true, ProjectionType.AdHocProvision);
                    spids.Union(dsref.generalSqlInformation.GetLoggerIdsByUuids(filterProjectionActivity_Projections_AdHocLst.ToArray()));
                }

                foreach (string spid in spids)
                    if (!filterByActivitySpidHashLst.Contains(spid))
                        filterByActivitySpidHashLst.Add(spid);



                //we should filter by Sync jobs, but no single job was specified: include ALL
                if (takeAllSync)
                {
                    spids = dsref.projectionActivity.GetLoggerIdsByUuids(new string[] { "*" }, true, ProjectionType.SyncGeneral);
                    spids.Union(dsref.generalSqlInformation.GetLoggerIdsByUuids(new string[] { "*" }));
                }
                else
                {
                    spids = dsref.projectionActivity.GetLoggerIdsByUuids(filterProjectionActivity_Projections_SyncLst.ToArray(), true, ProjectionType.SyncGeneral);
                    spids.Union(dsref.generalSqlInformation.GetLoggerIdsByUuids(filterProjectionActivity_Projections_SyncLst.ToArray()));
                }

                foreach (string spid in spids)
                    if (!filterByActivitySpidHashLst.Contains(spid))
                        filterByActivitySpidHashLst.Add(spid);
            }


            if (isfilterEnabled_JobServiceActivity)
            {
                spids = new string[] { };

                if (takeAllJsJob)
                    spids = dsref.jobserviceActivities
                                    .jobserviceJobs
                                    .Select(j => j.uidJob);

                else if (isfilterEnabled_JobServiceActivity_ByComponent)   //filter by component but do not simply take all                        
                                                                           //pick those which are selected
                    spids = dsref.jobserviceActivities
                                    .jobserviceJobs
                                    .Where(j => filterJobServiceActivity_ByComponentLst.Contains(j.taskfull))
                                    .Select(j => j.uidJob);

                else if (isfilterEnabled_JobServiceActivity_ByQueue)       //filter by queue but do not simply take all    
                                                                           //pick those which are selected
                    spids = dsref.jobserviceActivities
                                    .jobserviceJobs
                                    .Where(j => filterJobServiceActivity_ByQueueLst.Contains(j.queuename))
                                    .Select(j => j.uidJob);

                foreach (string spid in spids)
                    if (!filterByActivitySpidHashLst.Contains(spid))
                        filterByActivitySpidHashLst.Add(spid);
            }
        }

        public string ExportAsJson()
        {
            var jssett = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ContractResolver = new ExportSettingsJsonContractResolver(jsonExportTakeAttributeLst, null)
            };

            return JsonConvert.SerializeObject(this, jssett);
        }

        public MessageMatchResult IsMessageMatch(TextMessage msg, object additionalData)
        {
            //check incoming SPID ... 
            
            if (isfilterByActivity_SpidFilter_passUnseen)
                return MessageMatchResult.filterNotApplied;

            bool incomingSpidIsEmpty = string.IsNullOrEmpty(msg.spid);

            if (!incomingSpidIsEmpty && filterByActivitySpidHashLst.Contains(msg.spid))
                return MessageMatchResult.positive;

            //exception: what happens if our filter attribute has no value? InScope or OutScope ??
            if (incomingSpidIsEmpty)
                return MessageMatchResult.negative;

            return MessageMatchResult.negative;
        }
    }
}