using Microsoft.OData.Client;
using WorkZone;
using WorkZone.OData4.Client.Extensions;
using WorkZone.OData4.Client.UriFunction.Extensions;
using File = WorkZone.File;

namespace OnBehalfOfAsyncOData4
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // Example 1: Create a new case and search for it
                var newCaseId = await CreateCaseWithTextAsync();
                await ShowCaseTitleAsync(newCaseId);

                // Example 2: Add a contact to an existing case - the contact must exist in the system, and the case must exist. The contact is identified by NameType and NameCode.
                // NameType M is an Employee type, and NameCode @Me is a special code that identifies the current user. This will add the current user as a party to the case.
                await AddPartyToCaseAsync(newCaseId, "M", "@Me");

                // Example 3: Create a document and record, and archive them on an existing case
                await AddDocumentToCaseAsync(newCaseId);

                // Example 4: Create a contact and associate a case to it
                var newContactId = await CreateContactOnCaseAsync(newCaseId);

                // Example 5: Create a subcase 
                await CreateSubCaseOnCaseAsync(newCaseId);

                // Example 6: Set a value in a custom field
                await UpdateAmountOnCaseAsync(newCaseId);

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
                Console.WriteLine(ex.ToString());
            }

            Console.ReadLine();
        }

        static async Task<string> CreateCaseWithTextAsync()
        {
            // Set up OData context including Entra ID authorization. Keep the context short-lived to avoid it tracking too many entities.
            OData ctx = new();

            // Both File, Record and Contact have mandatory fields for creation. For files it is FileClass and Title.
            File newCase = new()
            {
                FileClass_Value = "00",
                Title = "Test Case",
                Text = new() { Text = "This is a test case created via OData4 client." }
            };
            
            // Attach the case to a DataServiceCollection in order for OData to track it and send it to the server on save.
            ctx.CreateDataServiceCollection(newCase);

            // Set up a projector to return both ID and FileNo on save. If no projector is given only ID will be returned.
            ctx.SetProjector(newCase, f => new() { ID = f.ID, FileNo = f.FileNo });

            // Calling SaveChangesBatchAsync() will send the new case and its File.Text in a single $batch request - mapping to a single transaction on the server and return the ID and FileNo of the new case.
            await ctx.SaveChangesBatchAsync();

            Console.WriteLine($"New case created with ID: {newCase.ID} and file number: {newCase.FileNo}");
            return newCase.ID;
        }

        static async Task ShowCaseTitleAsync(string caseId)
        {
            OData ctx = new();

            // Requesting ByKey with a Select projection to only return the Title of the case - 404 Not Found exception will be thrown if the case does not exist. 
            File caseFile = await ctx.Files.ByKey(caseId).Select(f => new File { Title = f.Title }).GetValueAsync();
            
            Console.WriteLine($"Case Title: {caseFile?.Title}");
        }

        static async Task AddPartyToCaseAsync(string caseId, string nameType, string nameCode)
        {
            // Create a new OData context. The context is short-lived to avoid it tracking too many entities.
            // The context is created with an OnBehalfOf user, which will make the server execute the requests on behalf of the specified user.
            // The user must have access to the case and the contact. SYSADM is just an example user that exist on most systems. In a real scenario the user should be a real user in the system.
            OData ctx = new();
            ctx.ODataRequestFeatures.OnBehalfOfUser = "SYSADM";

            // Get the case by ID. Without projection (select) only the ID property is returned. The case is added to a DataServiceCollection to have it tracked by the ctx.
            var file = (await ctx.Files.ByKey(caseId).ToDataServiceCollectionAsync()).Single();

            // Get the contact identified by nameType and nameCode.
            var queryContact = from c in ctx.Contacts
                               where c.NameType_Value == nameType && c.NameCode == nameCode
                               select c;

            // The contact is added to a DataServiceCollection to have it tracked by the ctx. Again only ID property is returned without projection (select).
            var contact = (await queryContact.ToDataServiceCollectionAsync()).SingleOrDefault();

            if (contact == null)
            {
                Console.WriteLine($"Contact with NameType {nameType} and NameCode {nameCode} not found.");
                return;
            }

            // Add the contact to the case's Parties subtable. The CustomLabel_Value is set to "Sagspart" to indicate the role of the contact in relation to the case.
            file.Parties.Add(new() { Name = contact, CustomLabel_Value = "Sagspart" });

            // Save the changes to the server. This will create a new FileContact entity linking the case and the contact.
            await ctx.SaveChangesAsync();

            Console.WriteLine($"Contact with NameType {nameType} and NameCode {nameCode} added to case {caseId}. ID of the party is {file.Parties.Last().ID}.");
        }

        static async Task AddDocumentToCaseAsync(string caseId)
        {
            // Record is the entity that contains metadata for a document. The document itself is stored as entity Document, on the Record.
            OData ctx = new();

            // First fetch the specific case for the record. It is added to a DataServiceCollection to have it tracked by the ctx. Without projection (select) only the ID property is returned.
            var file = (await ctx.Files.ByKey(caseId).ToDataServiceCollectionAsync()).Single();

            // Then create a text document with contents "ækldjhgdæfkhg".
            // Note that the document entity needs to be saved separately non batched before the record entity.
            Document doc = new();
            ctx.CreateDataServiceCollection(doc);
            ctx.SetSaveStream(doc, new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("ækldjhgdæfkhg")), true, "text/plain", "document.txt");
            // Uploading document...
            await ctx.SaveChangesAsync();

            // Now create a record and associate it with the document and the case.
            Record rec = new()
            {
                Document = doc,
                Title = "test document",
                State_Value = "ARK",
                RecordType_Value = "I",
                RecordGrp_Value = "ANS"
            };
            file.FileRecords.Add(rec);
            await ctx.SaveChangesBatchAsync();

            Console.WriteLine($"Record with ID {rec.ID} added to case {caseId}.");

            /* Alternatively the record can be linked to the case by Record.File if that is preferred. 
             * In that case the record needs to be added to a DataServiceCollection to have it tracked by the ctx.
             * Like this:
             * 
             * Record rec = new()
            {
                File = file,
                Document = doc,
                Title = "test document",
                State_Value = "ARK",
                RecordType_Value = "I",
                RecordGrp_Value = "ANS"
            };
            ctx.CreateDataServiceCollection(rec);
            await ctx.SaveChangesBatchAsync();
            */
        }

        static async Task<string> CreateContactOnCaseAsync(string caseId)
        {
            OData ctx = new();

            // Create a new contact and associate it with the case.
            Contact contact = new()
            {
                NameType_Value = "F",
                Name1 = "Barbar Bar"
            };
            ctx.CreateDataServiceCollection(contact);

            // Then save it using a the Files subtable
            FileContact fileContact = new() { FileKey_Value = caseId, Name = contact, CustomLabel_Value = "Sagspart" };
            contact.Files.Add(fileContact);

            // Send new contact and its association to the case as a party in a single $batch request - mapping to a single transaction on the server.
            await ctx.SaveChangesBatchAsync();

            Console.WriteLine($"Contact with ID {contact.ID} added to case {caseId}.");
            return contact.ID;
        }

        static async Task CreateSubCaseOnCaseAsync(string caseId)
        {
            OData ctx = new();

            // Create a new subcase/childcase on an existing case.
            File file = (await ctx.Files.ByKey(caseId).ToDataServiceCollectionAsync()).Single();

            File subCase = new()
            {
                FileClass_Value = "00",
                Title = "Test Subcase"
            };

            // Add it as a child to file using the FileRefs relation table. Undersag is the Danish term for subcase. The CustomLabel_Value is used to indicate the relation type between the parent case and the subcase.
            file.FileRefs.Add(new() { FileRef = subCase, CustomLabel_Value = "Undersag" });

            // If we need other properties than the ID of the new subCase returned we can setup a projector to return the properties we want. In this case we want the FileNo of the subcase returned as well.
            ctx.SetProjector(subCase, f => new() { ID = f.ID, FileNo = f.FileNo });
            ctx.SetProjector(file.FileRefs.First(), fr => new() { ID = fr.ID, FileRefKey_Value = fr.FileRefKey_Value });

            // Now save the new case and its association as a single $batch request - mapping to a single transaction on the server. Note that only the ID of the subcase is returned from SaveChanges, not the FileNo.
            await ctx.SaveChangesBatchAsync();

            Console.WriteLine($"New subcase created with ID: {subCase.ID} and file number: {subCase.FileNo}.");
        }

        static async Task DeleteContactAsync(string contactId)
        {
            OData ctx = new();

            // Delete a contact by ID. First it needs to be cleaned of references to other entities, then it can be deleted.
            // Set up DataServiceCollection to track the contact and its references. In this example there is only a single reference to Files.
            var fileParties = (await ctx.FileContacts.Where(fc => fc.NameKey_Value == contactId).ToDataServiceCollectionAsync());
            
            // If the contact are a party of multiple cases loop thru then and remove them all...
            foreach (var fileParty in fileParties.ToList())
            {
                fileParties.Remove(fileParty);
            }
            
            var contacts = (await ctx.Contacts.ByKey(contactId).ToDataServiceCollectionAsync());
            contacts.Remove(contacts.Single());

            await ctx.SaveChangesBatchAsync();

            Console.WriteLine($"Contact with ID: {contactId} deleted.");
        }

        static async Task UpdateAmountOnCaseAsync(string caseId)
        {
            OData ctx = new();

            // This sample assumes that a custom property Amount of type Int64 have been defined on the File type. Update the custom property Amount on the case identified by caseId.
            var file = (await ctx.Files.ByKey(caseId).ToDataServiceCollectionAsync()).Single();
            file.Amount = 1000000;

            await ctx.SaveChangesBatchAsync();
        }

        static async Task UpdateCasesCreatedTodayAsync()
        {
            OData ctx = new();

            // Update all cases created today to have a title "Updated today".
            // By default WorkZone OData returns the first 50 instances of a query. To get more paging needs to be used.
            var files = await ctx.Files.Where(f => f.Created >= DateOnly.FromDateTime(DateTime.Today).AddDays(-1)).ToDataServiceCollectionAsync();

            // LoadAllPages() implements paging thru the nextLink/continuation returned by the service.
            await files.LoadAllPagesAsync(ctx);

            foreach (var file in files)
            {
                file.Title = "Updated at time " + DateTime.Now.ToString("HH:mm:ss");                    
            }

            await ctx.SaveChangesBatchAsync();

            Console.WriteLine("Updated the " + files.Count + " cases that were created today.");
        }

        static async Task UpdateCaseForProjectionAsync()
        {
            OData ctx = new();

            // 1: Create two cases, one to show projection is needed, and a second that has projection.
            File file1 = new()
            {
                FileClass_Value = "00",
                Title = "Case1",
                Dates = new(ctx)
                {
                    new() { CustomLabel_Value = "Sagsdato1", DateStamp = DateOnly.FromDateTime(DateTime.Today) }
                }
            };
            File file2 = new()
            {
                FileClass_Value = "00",
                Title = "Case2",
                Dates = new(ctx)
                {
                    new() { CustomLabel_Value = "Sagsdato1", DateStamp = DateOnly.FromDateTime(DateTime.Today) }
                }
            };

            // Attach the cases to a DataServiceCollection in order for the DataServiceContext to track it and send it to the server on save.
            ctx.CreateDataServiceCollection(file1, file2);

            // Save the 2 new cases and their Dates in a single $batch request - mapping to a single transaction on the server.
            await ctx.SaveChangesBatchAsync();

            // Get a new DataServiceContext that doesn't already track the 2 cases.
            ctx = new();

            // now update file1's file date with 974 years in the future. Projection is needed to get the Dates subtable.
            // The projection will automatically be transformed into the OData $select and $expand query options.
            // This query shows how to do projection on multiple levels of the entity graph. The FileDate is a subtable of File, and the CustomLabel_Value and DateStamp are properties of FileDate.
            var query = from f in ctx.Files.ByKey(file1.ID)
                        select new File
                        {
                            ID = f.ID,
                            Dates = new(
                                from d in f.Dates
                                select new FileDate
                                { 
                                    ID = d.ID,
                                    CustomLabel_Value = d.CustomLabel_Value, 
                                    DateStamp = d.DateStamp 
                                })
                        };

            // Now fetch the case with its Dates subtable. The Dates subtable will be populated with the projected properties.
            file1 = (await query.ToDataServiceCollectionAsync()).Single();

            foreach (var date in file1.Dates)
            {
                Console.WriteLine($"Updating File Date {date.ID} {date.CustomLabel_Value} {date.DateStamp}");
                date.DateStamp = DateOnly.FromDateTime(DateTime.Now).AddYears(974);
            }

            // Save the changes to the server. This will update all the FileDate entities in the Dates subtable of the case (there is only one in this example).
            await ctx.SaveChangesBatchAsync();

            Console.WriteLine($"Updated the file dates of case {file1.ID}.");
        }

        static async Task SearchCaseByDomain()
        {
            OData ctx = new();

            var q = from f in ctx.Files
                    where ServerDomain.In("my_open_cases")
                    select f;

            Console.WriteLine($"Files found: {q.Count()}");
        }

    }
}
