using Microsoft.OData.Client;
using Microsoft.VisualBasic;
using System;
using System.Linq;
using System.Security.AccessControl;
using WorkZone;
using WorkZone.OData4.Client;
using WorkZone.OData4.Client.Extensions;
using static System.Net.WebRequestMethods;
using File = WorkZone.File;

namespace OnBehalfOfAsyncOData4
{
    internal class Program
    {
// missing subjects for general (not AAU): domain, access code, 
        static async Task Main(string[] args)
        {
            try
            {
                // Example 1: Create a new case and search for it
                var newCaseId = await CreateCaseWithTextAsync();
                await ShowCaseTitleAsync(newCaseId);

                // Example 2: Add a contact to an existing case
                await AddContactToCaseAsync(newCaseId, "I", "1");

                // Example 3: Create a document and record, and archive them on an existing case
                await AddDocumentToCaseAsync(newCaseId);

                // Example 4: Create a contact and associate a case to it
                var newContactId = await CreateContactOnCaseAsync(newCaseId);

                // Example 5: Create a subcase 
                await CreateSubCaseOnCaseAsync(newCaseId);

                // Example 6: Upsert on a custom field
                await UpdateBEVSTOnCaseAsync(newCaseId);

                // Example 7: Update 60 newest cases to show paging
                await UpdateCasesCreatedTodayAsync();

                // Example 8: Update a case to show projection
                await UpdateCaseForProjectionAsync();

                // Example 9: Delete the contact created in Example 4
                await DeleteContactAsync(newContactId);

                // Example 10: Search for cases in domain "my_open_cases"
                await SearchCaseByDomain();

                Console.WriteLine("Finished.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            Console.ReadLine();
        }

        static async Task<string> CreateCaseWithTextAsync()
        {
            //set up Odata context including Entra ID authorization. Establish context locally to avoid lugging a memory-heavy context around.
            var context = new OData();

            // Both File, Record and Contact have mandatory fields for creation. For files it is FileClass_Value and Title.
            File newCase = new File
            {
                FileClass_Value = "00",
                Title = "Test Case",
                Text = new Group_File_Text { Text = "This is a test case created via OData4 client." }
            };
            // Attach the case to a DataServiceCollection in order for OData to track it and send it to the server on save.
            context.CreateDataServiceCollection(newCase);
            await context.SaveChangesBatchAsync();           // Cannot be used with OnBehalfOf via Confidential Client
            Console.WriteLine($"New case created with ID: {newCase.ID} and file number: {newCase.FileNo} (note it is empty because only ID is returned from SaveChanges)");
            return newCase.ID;
        }

        static async Task ShowCaseTitleAsync(string caseId)
        {
            var context = new OData();
            var caseFile = await context.Files.ByKey(caseId).Select(f => new { f.Title }).GetValueAsync();
            

            Console.WriteLine($"Case Title: {caseFile?.Title}");
        }

        static async Task AddContactToCaseAsync(string caseId, string nameType, string nameCode)
        {
            var context = new OData();
            var trackedFiles = new DataServiceCollection<File>(context);
            // Uffe: with the following variant the file is lost, it's not possible to access the properties: 
            // var trackedFile = context.Files.ByKey(caseId).ToDataServiceCollection();
            var file = await Task.Run(() => context.Files.Where(f => f.ID == caseId).FirstOrDefault());
            trackedFiles.Add(file);
            var query = from c in context.Contacts
                        where c.NameType_Value == nameType && c.NameCode == nameCode
                        select new Contact
                        {
                            ID = c.ID,
                            AddressKey_Value = c.AddressKey_Value
                        };
            var contact = await Task.Run(() => query.FirstOrDefault<Contact>());

            FileContact fileContact = new FileContact { AddressKey_Value = contact?.AddressKey_Value, CustomLabel_Value = "Sagspart", NameKey_Value = contact?.ID };
            
            file.Parties.Add(fileContact);
            await context.SaveChangesAsync();
        }

        static async Task AddDocumentToCaseAsync(string caseId)
        {
            // Record is the entity that contains metadata for a document. The document itself is stored as entity Document, on the Record.
            // First set up a specific file for the record.
            var context = new OData();
            var trackedFiles = new DataServiceCollection<File>(context);
            var file = await Task.Run(() => context.Files.Where(f => f.ID == caseId).FirstOrDefault());
            trackedFiles.Add(file);

            // Then create a text document with contents "ækldjhgdæfkhg".
            // Note that the document entity needs to be saved separately before the record entity.

            var doc = new Document();
            context.AddToDocuments(doc);
            context.SetSaveStream(doc, new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("ækldjhgdæfkhg")), "document.txt");
            await context.SaveChangesAsync();

            // Now create a record and associate it with the document and the case.
            Record rec = new Record
            {
                Document = doc,
                Title = "test document",
                State_Value = "ARK",
                RecordType_Value = "I",
                RecordGrp_Value = "ANS"
            };
            file.FileRecords.Add(rec);
            await context.SaveChangesAsync();
        }
        static async Task<string> CreateContactOnCaseAsync(string caseId)
        {
            // Create a new contact and associate it with the case.
            var context = new OData();
            var trackedContacts = new DataServiceCollection<Contact>(context);
            Contact contact = new Contact
            {
                NameType_Value = "F",
                Name1 = "Barbar Bar"
            };
            trackedContacts.Add(contact);
            await context.SaveChangesAsync();

            // Then save it using a the Files subtable
            var fileContact = new FileContact {FileKey_Value=caseId, AddressKey_Value = contact.AddressKey_Value, CustomLabel_Value = "Sagspart", NameKey_Value = contact.ID };
            contact.Files.Add(fileContact);
            await context.SaveChangesAsync();
            return contact.ID;
        }

        static async Task CreateSubCaseOnCaseAsync(string caseId)
        {
            // Create a subcase on an existing case.
            var context = new OData();
            var trackedFiles = new DataServiceCollection<File>(context);
            var file = await Task.Run(() => context.Files.Where(f => f.ID == caseId).FirstOrDefault());
            trackedFiles.Add(file);
            File subCase = new File
            {
                FileClass_Value = "00",
                Title = "Test Subcase"
            };
            trackedFiles.Add(subCase);
            await context.SaveChangesAsync();
            // Then save it using the FileRefs subtable
            file.FileRefs.Add(new FileFile { FileKey_Value= caseId, FileRefKey_Value = subCase.ID, CustomLabel_Value = "Undersag" });     // Custom Label Undersag is used for list Undersager
            await context.SaveChangesBatchAsync();
            Console.WriteLine($"New subcase created with ID: {subCase.ID} and file number: {subCase.FileNo} (note it is empty because only ID is returned from SaveChanges)");
        }

        static async Task DeleteContactAsync(string contactId)
        {
            // Delete a contact by ID. First it needs to be cleaned of references to other entities, then it can be deleted.
            // Set up DataServiceCollection to track the contact and its references. In this example there is only a single reference to Files.
            var context = new OData();
            var trackedContacts = new DataServiceCollection<Contact>(context);
            var trackedFileContacts = new DataServiceCollection<FileContact>(context);
            // Get the reference to Files as well as the contact.
            var fileContact = await Task.Run(() => context.FileContacts.Where(c => c.NameKey_Value == contactId).FirstOrDefault());
            var contact = await Task.Run(() => context.Contacts.Where(c => c.ID == contactId).FirstOrDefault());
            // Add them to the DataServiceCollection so they are tracked by OData. Followed by deletion. SaveChangesBatch is used to collate the deletes into a single request to the server.
            trackedFileContacts.Add(fileContact);
            context.DeleteObject(fileContact);
            trackedContacts.Add(contact);
            context.DeleteObject(contact);
            await context.SaveChangesBatchAsync();
        }

        static async Task UpdateBEVSTOnCaseAsync(string caseId)
        {
            // Update the custom field BEVST on the case. Notice that custom fields are named with capital letters.
            var context = new OData();
            var trackedFiles = new DataServiceCollection<File>(context);
            var file = await Task.Run(() => context.Files.Where(f => f.ID == caseId).FirstOrDefault());
            trackedFiles.Add(file);
            file.BEVST = 1000000;
            await context.SaveChangesBatchAsync();
        }

        static async Task UpdateCasesCreatedTodayAsync()
        {
            // Update all cases created today to have a title "Updated today".
            // By default WorkZone OData returns the first 50 instances of a query. To get more continuation needs to be used.
            var context = new OData();
            var trackedFiles = new DataServiceCollection<File>(context);
            var files = await Task.Run(() => context.Files.Where(f => f.Created >= DateTime.Today.AddDays(-1)).ToDataServiceCollection());
            // LoadAllPages() implements continuation. It collects all data in a single OData request.
            await Task.Run(() => files.LoadAllPages(context));

            foreach (var file in files)
            {
                trackedFiles.Add(file);
                file.Title = "Updated at time " + DateTime.Now.ToString("HH:mm:ss");                    
            }
            await context.SaveChangesBatchAsync();
        }

        static async Task UpdateCaseForProjectionAsync()
        {
            // Projection is needed on subtables, but not on fields directly on the entity.

            // 1: Create two cases, one to show projection is needed, and a second that has projection.
            var context = new OData();
            var trackedFiles = new DataServiceCollection<File>(context);
            File file1 = new File
            {
                FileClass_Value = "00",
                Title = "Case1",
                Dates = new DataServiceCollection<FileDate>(context)
                {
                    new FileDate { CustomLabel_Value = "Sagsdato1", DateStamp = DateTime.Today }
                }
            };
            File file2 = new File
            {
                FileClass_Value = "00",
                Title = "Case2",
                Dates = new DataServiceCollection<FileDate>(context)
                {
                    new FileDate { CustomLabel_Value = "Sagsdato1", DateStamp = DateTime.Today }
                }
            };
            // Attach the case to a DataServiceCollection in order for OData to track it and send it to the server on save.
            trackedFiles.Add(file1);
            trackedFiles.Add(file2);
            await context.SaveChangesBatchAsync();
            trackedFiles = null;
            context = null;

            context = new OData();
            trackedFiles = new DataServiceCollection<File>(context);
            // now update the filedate with 974 years in the future to show that it is possible to update a date. .Select new File is necessary to get the Dates subtable, otherwise it is not included in the query and cannot be updated.
            var files = await Task.Run(() => context.Files.Where(f => f.ID == file1.ID).Select(f => new File{Dates = f.Dates }).ToDataServiceCollection());
            foreach (var file in files)
            {
                trackedFiles.Add(file);
                foreach (var date in file.Dates)
                {
                    date.DateStamp = DateTime.Now.AddYears(974);
                }
                ;
            };
            await context.SaveChangesBatchAsync();
        }
        static async Task SearchCaseByDomain()
        {
            var context = new OData();
            var q = context
            .CreateQuery<File>("Files")
            .AddQueryOption("$filter", "inDomain('my_open_cases')")
            .AddQueryOption("$select", "ID,Officer_Value");

            Console.WriteLine($"Files found: {q.Count()}");
        }

    }
}
