using System;
using System.Collections.Generic;
using System.Text;
using Outlook = Microsoft.Office.Interop.Outlook;
using Google.GData.Extensions;
using Google.GData.Calendar;
using System.Collections;

using System.Runtime.InteropServices;
using System.IO;

namespace GoContactSyncMod
{
    internal static class AppointmentPropertiesUtils
    {
        public static string GetOutlookId(Outlook.AppointmentItem outlookAppointment)
        {
            return outlookAppointment.EntryID;
        }
        public static string GetGoogleId(EventEntry googleAppointment)
        {
            string id = googleAppointment.Id.ToString();
            if (id == null)
                throw new Exception();
            return id;
        }

        public static void SetGoogleOutlookAppointmentId(string syncProfile, EventEntry googleAppointment, Outlook.AppointmentItem outlookAppointment)
        {
            if (outlookAppointment.EntryID == null)
                throw new Exception("Must save outlook Appointment before getting id");

            SetGoogleOutlookAppointmentId(syncProfile, googleAppointment, GetOutlookId(outlookAppointment));
        }

        public static void SetGoogleOutlookAppointmentId(string syncProfile, EventEntry googleAppointment, string outlookAppointmentId)
        {
            // check if exists
            bool found = false;
            foreach (var p in googleAppointment.ExtensionElements)
            {
                if (p is ExtendedProperty && ((ExtendedProperty)p).Name == "gos:oid:" + syncProfile + "")
                {
                    ((ExtendedProperty)p).Value = outlookAppointmentId;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                Google.GData.Extensions.ExtendedProperty prop = new ExtendedProperty(outlookAppointmentId, "gos:oid:" + syncProfile + "");
                prop.Value = outlookAppointmentId;
                googleAppointment.ExtensionElements.Add(prop);
            }
        }
        public static string GetGoogleOutlookAppointmentId(string syncProfile, EventEntry googleAppointment)
        {
            // get extended prop
            foreach (var p in googleAppointment.ExtensionElements)
            {
                if (p is ExtendedProperty && ((ExtendedProperty)p).Name == "gos:oid:" + syncProfile + "")
                    return ((ExtendedProperty)p).Value;
            }
            return null;
        }
        public static void ResetGoogleOutlookAppointmentId(string syncProfile, EventEntry googleAppointment)
        {
            // get extended prop
            foreach (var p in googleAppointment.ExtensionElements)
            {
                if (p is ExtendedProperty && ((ExtendedProperty)p).Name == "gos:oid:" + syncProfile + "")
                {
                    // remove 
                    googleAppointment.ExtensionElements.Remove(p);
                    return;
                }
            }
        }

        /// <summary>
        /// Sets the syncId of the Outlook Appointment and the last sync date. 
        /// Please assure to always call this function when saving OutlookItem
        /// </summary>
        /// <param name="sync"></param>
        /// <param name="outlookAppointment"></param>
        /// <param name="googleAppointment"></param>
        public static void SetOutlookGoogleAppointmentId(Syncronizer sync, Outlook.AppointmentItem outlookAppointment, EventEntry googleAppointment)
        {
            if (googleAppointment.Id.Uri == null)
                throw new NullReferenceException("GoogleAppointment must have a valid Id");

            //check if outlook Appointment aready has google id property.
            Outlook.UserProperties userProperties = outlookAppointment.UserProperties;
            try
            {
                Outlook.UserProperty prop = userProperties[sync.OutlookPropertyNameId];
                if (prop == null)
                    prop = userProperties.Add(sync.OutlookPropertyNameId, Outlook.OlUserPropertyType.olText, true);
                try
                {
                    prop.Value = googleAppointment.Id.Uri.Content;
                }
                finally
                {
                    Marshal.ReleaseComObject(prop);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(userProperties);
            }

            //save last google's updated date as property
            /*prop = outlookAppointment.UserProperties[OutlookPropertyNameUpdated];
            if (prop == null)
                prop = outlookAppointment.UserProperties.Add(OutlookPropertyNameUpdated, Outlook.OlUserPropertyType.olDateTime, null, null);
            prop.Value = googleAppointment.Updated;*/

            //Also set the OutlookLastSync date when setting a match between Outlook and Google to assure the lastSync updated when Outlook Appointment is saved afterwards
            SetOutlookLastSync(sync, outlookAppointment);
        }

        public static void SetOutlookLastSync(Syncronizer sync, Outlook.AppointmentItem outlookAppointment)
        {
            //save sync datetime
            Outlook.UserProperties userProperties = outlookAppointment.UserProperties;
            try
            {
                Outlook.UserProperty prop = userProperties[sync.OutlookPropertyNameSynced];
                if (prop == null)
                    prop = userProperties.Add(sync.OutlookPropertyNameSynced, Outlook.OlUserPropertyType.olDateTime, true);
                try
                {
                    prop.Value = DateTime.Now;
                }
                finally
                {
                    Marshal.ReleaseComObject(prop);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(userProperties);
            }
        }

        public static DateTime? GetOutlookLastSync(Syncronizer sync, Outlook.AppointmentItem outlookAppointment)
        {
            DateTime? result = null;
            Outlook.UserProperties userProperties = outlookAppointment.UserProperties;
            try
            {
                Outlook.UserProperty prop = userProperties[sync.OutlookPropertyNameSynced];
                if (prop != null)
                {
                    try
                    {
                        result = (DateTime)prop.Value;
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(prop);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(userProperties);
            }
            return result;
        }
        public static string GetOutlookGoogleAppointmentId(Syncronizer sync, Outlook.AppointmentItem outlookAppointment)
        {
            string id = null;
            Outlook.UserProperties userProperties = outlookAppointment.UserProperties;
            try
            {
                Outlook.UserProperty idProp = userProperties[sync.OutlookPropertyNameId];
                if (idProp != null)
                {
                    try
                    {
                        id = (string)idProp.Value;
                        if (id == null)
                            throw new Exception();
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(idProp);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(userProperties);
            }
            return id;
        }
        public static void ResetOutlookGoogleAppointmentId(Syncronizer sync, Outlook.AppointmentItem outlookAppointment)
        {
            Outlook.UserProperties userProperties = outlookAppointment.UserProperties;
            try
            {
                Outlook.UserProperty idProp = userProperties[sync.OutlookPropertyNameId];
                try
                {
                    Outlook.UserProperty lastSyncProp = userProperties[sync.OutlookPropertyNameSynced];
                    try
                    {
                        if (idProp == null && lastSyncProp == null)
                            return;

                        List<int> indexesToBeRemoved = new List<int>();
                        IEnumerator en = userProperties.GetEnumerator();
                        en.Reset();
                        int index = 1; // 1 based collection            
                        while (en.MoveNext())
                        {
                            Outlook.UserProperty userProperty = en.Current as Outlook.UserProperty;
                            if (userProperty == idProp || userProperty == lastSyncProp)
                            {
                                indexesToBeRemoved.Add(index);
                                //outlookAppointment.UserProperties.Remove(index);
                                //Don't return to remove both properties, googleId and lastSynced
                                //return;
                            }
                            index++;
                            Marshal.ReleaseComObject(userProperty);
                        }

                        for (int i = indexesToBeRemoved.Count - 1; i >= 0; i--)
                            userProperties.Remove(indexesToBeRemoved[i]);
                        //throw new Exception("Did not find prop.");
                    }
                    finally
                    {
                        if (lastSyncProp != null)
                            Marshal.ReleaseComObject(lastSyncProp);
                    }
                }
                finally
                {
                    if (idProp != null)
                        Marshal.ReleaseComObject(idProp);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(userProperties);
            }
        }

                
    }
}