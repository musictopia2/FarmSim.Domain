using System;
using System.Collections.Generic;
using System.Text;

namespace FarmSim.Domain.Services.Automation.Worksites;

public enum EnumWorksitePreviewMode
{
    /// <summary>
    /// Use the actual active workers assigned to the worksite (runtime truth).
    /// </summary>
    AutomatedActiveWorkers,

    /// <summary>
    /// Planning view: use planned/pending workers, but exclude ones that are locked/unavailable.
    /// </summary>
    PlannedUnlockedWorkers,

    /// <summary>
    /// Planning view: show everything planned, even if locked (optional; handy for “what if” UI).
    /// </summary>
    PlannedAllWorkers,

    UIWorkersActive
}