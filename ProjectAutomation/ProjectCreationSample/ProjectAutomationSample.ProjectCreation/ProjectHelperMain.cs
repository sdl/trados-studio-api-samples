﻿namespace Sdl.ProjectAutomation.ProjectAutomationSample.ProjectCreation
{
    using Sdl.Core.Globalization;
    using Sdl.Core.Settings;
    using Sdl.ProjectApi.Settings;
    using Sdl.ProjectAutomation.Core;
    using Sdl.ProjectAutomation.FileBased;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class ProjectHelperMain
    {
        /// <summary>
        /// Create basic project from scratch, not using templates and add files from selected folder
        /// </summary>
        public void CreateProject(LocalProjectSettings settings)
        {
            try
            {
                ////Create new project object
                FileBasedProject createdProject = new FileBasedProject(
                    this.GetProjectInfo(
                    settings.ProjectName,
                        new Language(settings.SourceLanguage),
                        new Language[] { new Language(settings.TargetLanguage) }, settings.OutputPath));

                ////Add files from selected folder
                createdProject.AddFolderWithFiles(settings.InputFolder, true);

                ////Start the tasks
                this.RunTasks(createdProject, settings);

                createdProject.Save();
                ////project is saved but not listed in Studio, this is by design.
            }
            catch (Exception ex)
            {
                throw new Exception("Problem during project creation", ex);
            }
        }

        /// <summary>
        /// Create a new project from the seected project package
        /// </summary>
        /// <param name="settings"></param>
        public void CreateProjectFromPackage(LocalProjectSettings settings)
        {
            try
            {
                ProjectPackageImport projectPackageImport;
                var createdProject = FileBasedProject.CreateFromProjectPackage(settings.PackagePath, settings.OutputPath, out projectPackageImport);

                createdProject.Save();
            }
            catch (Exception ex)
            {
                throw new Exception("Problem during project creation", ex);
            }

        }

        /// <summary>
        /// Run defalut Batch Tasks after creating a new project
        /// </summary>
        /// <param name="createdProject">A project you have created</param>
        /// <param name="settings">Settings containing initial input parameters</param>
        private void RunTasks(FileBasedProject createdProject, LocalProjectSettings settings)
        {
            Language targetLanguage = new Language(settings.TargetLanguage);
            List<TaskStatusEventArgs> taskStatusEventArgsList = new List<TaskStatusEventArgs>();
            List<MessageEventArgs> messageEventArgsList = new List<MessageEventArgs>();

            // set up  perfect match
            ProjectFile[] projectFiles = createdProject.GetSourceLanguageFiles();
            createdProject.AddBilingualReferenceFiles(GetBilingualFileMappings(new Language[] { targetLanguage }, projectFiles, settings.OutputPath));

            // scan files
            AutomaticTask automaticTask = this.RunTasks(
                createdProject,
                projectFiles,
                AutomaticTaskTemplateIds.Scan,
                taskStatusEventArgsList,
                messageEventArgsList);

            this.CheckEvents(taskStatusEventArgsList, messageEventArgsList);

            // convert files
            automaticTask = this.RunTasks(
                createdProject,
                projectFiles,
                AutomaticTaskTemplateIds.ConvertToTranslatableFormat,
                taskStatusEventArgsList,
                messageEventArgsList);

            this.CheckEvents(taskStatusEventArgsList, messageEventArgsList);

            // copy files to target languages
            automaticTask = this.RunTasks(
                createdProject,
                projectFiles,
                AutomaticTaskTemplateIds.CopyToTargetLanguages,
                taskStatusEventArgsList,
                messageEventArgsList);

            this.CheckEvents(taskStatusEventArgsList, messageEventArgsList);

            // from now on use target language files
            projectFiles = createdProject.GetTargetLanguageFiles(targetLanguage);

            // Apply Perfect Match
            automaticTask = this.RunTasks(
                createdProject,
                projectFiles,
                AutomaticTaskTemplateIds.PerfectMatch,
                taskStatusEventArgsList,
                messageEventArgsList);

            this.CheckEvents(taskStatusEventArgsList, messageEventArgsList);

            // analyze files
            automaticTask = this.RunTasks(
                createdProject,
                projectFiles,
                AutomaticTaskTemplateIds.AnalyzeFiles,
                taskStatusEventArgsList,
                messageEventArgsList);

            this.CheckEvents(taskStatusEventArgsList, messageEventArgsList);

            // translate files
            automaticTask = this.RunTasks(
                createdProject,
                projectFiles,
                AutomaticTaskTemplateIds.PreTranslateFiles,
                taskStatusEventArgsList,
                messageEventArgsList);

            this.CheckEvents(taskStatusEventArgsList, messageEventArgsList);

            // populate project TM
            automaticTask = this.RunTasks(
                createdProject,
                projectFiles,
                AutomaticTaskTemplateIds.PopulateProjectTranslationMemories,
                taskStatusEventArgsList,
                messageEventArgsList);

            this.CheckEvents(taskStatusEventArgsList, messageEventArgsList);
        }



        /// <summary>
        /// Simple mapping routine to associate bilingual files in a previous project with the file in the current project
        /// Looks for a bilingual file with the same name in the relevant language directories 
        /// </summary>
        /// <remarks>
        ///  This routine is provided as a basic example of mapping previous documents to current documents. If a more complicated mapping 
        ///  is required (perhaps different versions have a version number in the filename) then you can build in your own rules to do this. 
        ///  
        /// Example: 
        ///     languages = { "fr-FR", "de-DE" }
        ///     files = { "file1.docx", "file2.docx" }
        ///     PreviousProjectPath = "C:\Projects\MyOldProject"
        ///     
        /// This routine will look for and associate the following files as bilingual reference files if present
        ///     "c:\Projects\MyOldProject\fr-FR\file1.docx.sdlxliff"     
        ///     "c:\Projects\MyOldProject\fr-FR\file2.docx.sdlxliff"
        ///     "c:\Projects\MyOldProject\de-DE\file1.docx.sdlxliff"
        ///     "c:\Projects\MyOldProject\de-DE\file2.docx.sdlxliff"
        /// </remarks>
        /// <param name="TargetLanguages">An array of target languages</param>
        /// <param name="TranslatableFiles">An array of project files </param>
        /// <param name="PreviousProjectPath">The root directory of the previous SDL Studio Project</param>
        public BilingualFileMapping[] GetBilingualFileMappings(Language[] targetLanguages, ProjectFile[] translatableFiles, string previousProjectPath)
        {
            List<BilingualFileMapping> mappings = new List<BilingualFileMapping>();
            foreach (Language language in targetLanguages)
            {
                string searchPath = Path.Combine(previousProjectPath, language.IsoAbbreviation);
                foreach (ProjectFile file in translatableFiles)
                {
                    string previousFile = String.Concat(Path.Combine(searchPath, file.Name), (file.Name.EndsWith(".sdlxliff") ? "" : ".sdlxliff"));
                    if (File.Exists(previousFile))
                    {
                        BilingualFileMapping mapping = new BilingualFileMapping()
                        {
                            BilingualFilePath = previousFile,
                            Language = language,
                            FileId = file.Id
                        };
                        mappings.Add(mapping);
                    }
                }
            }
            return mappings.ToArray();
        }



        private void CheckEvents(List<TaskStatusEventArgs> taskStatusEventArgsList, List<MessageEventArgs> messageEventArgsList)
        {
            // task statuses and messages can be iterated and any problems can be reported
            foreach (var item in taskStatusEventArgsList)
            {
                switch (item.Status)
                {
                    case TaskStatus.Assigned:
                        break;
                    case TaskStatus.Cancelled:
                        break;
                    case TaskStatus.Cancelling:
                        break;
                    case TaskStatus.Completed:
                        break;
                    case TaskStatus.Created:
                        break;
                    case TaskStatus.Failed:
                        break;
                    case TaskStatus.Invalid:
                        break;
                    case TaskStatus.Rejected:
                        break;
                    case TaskStatus.Started:
                        break;
                    default:
                        break;
                }
            }

            // at the end clear task statuses and messages
            taskStatusEventArgsList.Clear();
            messageEventArgsList.Clear();
        }

        private AutomaticTask RunTasks(
            FileBasedProject createdProject,
            ProjectFile[] projectFiles,
            string taskIDToRun,
            List<TaskStatusEventArgs> taskStatusEventArgsList,
            List<MessageEventArgs> messageEventArgsList)
        {
            AutomaticTask task = createdProject.RunAutomaticTask(
                projectFiles.GetIds(),
                taskIDToRun,
                (sender, taskStatusArgs) =>
                {
                    taskStatusEventArgsList.Add(taskStatusArgs);
                },
                (sender, messageArgs) =>
                {
                    messageEventArgsList.Add(messageArgs);
                });

            return task;
        }

        private ProjectInfo GetProjectInfo(string projectName, Language sourceLang, Language[] targetLangs, string path)
        {
            ProjectInfo newProjectInfo = new ProjectInfo()
            {
                Name = projectName,
                CreatedBy = "API automation",
                Description = "Project created by API",
                DueDate = DateTime.Now.AddDays(7),
                SourceLanguage = sourceLang,
                TargetLanguages = targetLangs,
                LocalProjectFolder = path
            };

            return newProjectInfo;
        }
    }
}
