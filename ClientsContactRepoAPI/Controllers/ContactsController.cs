using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Description;
using ClientContactsRepoDbLibrary;
using ClientsContactRepoAPI.Models;

namespace ClientsContactRepoAPI.Controllers
{
    public class ContactsController : ApiController
    {
        private ContactsRepoEntities dbEntities = new ContactsRepoEntities();

        // GET api/contacts
        [ResponseType(typeof(List<ClientsContactsModel>))]
        [HttpGet]
        public IHttpActionResult Get()
        {
            List<ClientsContactsModel> clientsDetails = (from cd in dbEntities.ClientDetails
                                                         join cad in dbEntities.ClientAddressDetails on cd.Id equals cad.ClientId
                                                         select new ClientsContactsModel
                                                         {
                                                             id = cd.Id,
                                                             name = new Name { first = cd.First, middle = cd.Middle, last = cd.Last },
                                                             address = new Address { street = cad.Street, city = cad.City, state = cad.State, zip = cad.Zip },
                                                             email = cad.Email
                                                         }).Distinct().ToList();

            if (clientsDetails != null)
            {
                foreach (var client in clientsDetails)
                {
                    client.phone = (from pd in dbEntities.PhoneDetails
                                    join config in dbEntities.ClientRepoConfigs on pd.PhoneType equals config.ConfigValue
                                    where config.ConfigType == "PhoneType" && pd.ClientId == client.id
                                    select new Phone { number = pd.PhoneNumber, type = config.ConfigKey }).Distinct().OrderBy(o => o.type).ToList();
                }
            }

            if (clientsDetails == null)
            {
                return NotFound();
            }

            return Ok(clientsDetails);
        }

        // GET api/contacts/1
        [ResponseType(typeof(ClientsContactsModel))]
        [HttpGet]
        public IHttpActionResult Get(int Id)
        {
            ClientsContactsModel clientsDetails = (from cd in dbEntities.ClientDetails
                                                   join cad in dbEntities.ClientAddressDetails on cd.Id equals cad.ClientId
                                                   where cd.Id == Id
                                                   select new ClientsContactsModel
                                                   {
                                                       id = cd.Id,
                                                       name = new Name { first = cd.First, middle = cd.Middle, last = cd.Last },
                                                       address = new Address { street = cad.Street, city = cad.City, state = cad.State, zip = cad.Zip },
                                                       email = cad.Email
                                                   }).FirstOrDefault();

            if (clientsDetails != null)
            {
                clientsDetails.phone = (from pd in dbEntities.PhoneDetails
                                        join config in dbEntities.ClientRepoConfigs on pd.PhoneType equals config.ConfigValue
                                        where pd.ClientId == Id && config.ConfigType == "PhoneType"
                                        orderby config.ConfigKey
                                        select new Phone { number = pd.PhoneNumber, type = config.ConfigKey }).OrderBy(o => o.type).ToList();
            }

            if (clientsDetails == null)
            {
                return NotFound();
            }

            return Ok(clientsDetails);
        }

        // POST api/contacts
        [HttpPost]
        public IHttpActionResult Post(ClientsContactsModel client)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Request");
            }

            using (var context = new ContactsRepoEntities())
            {
                context.ClientDetails.Add(
                    new ClientDetail
                    {
                        First = client.name.first,
                        Middle = client.name.middle,
                        Last = client.name.last,
                    });

                context.SaveChanges();

                int newClientId = context.ClientDetails.OrderByDescending(x => x.Id).Select(x => x.Id).FirstOrDefault();

                context.ClientAddressDetails.Add(
                    new ClientAddressDetail
                    {
                        Street = client.address.street,
                        City = client.address.city,
                        State = client.address.state,
                        Zip = client.address.zip,
                        Email = client.email,
                        ClientId = newClientId
                    });

                foreach (var phone in client.phone)
                {
                    context.PhoneDetails.Add(
                        new PhoneDetail
                        {
                            PhoneNumber = phone.number,
                            PhoneType = GetPhoneType(phone.type),
                            ClientId = newClientId
                        });
                    context.SaveChanges();
                }
            }

            return Ok();
        }

        // PUT api/values/5
        [HttpPut]
        public void Put(int id, ClientsContactsModel client)
        {
            var clientDetail = (from cd in dbEntities.ClientDetails where cd.Id == id select cd).FirstOrDefault();
            if (clientDetail != null)
            {
                clientDetail.First = client.name.first;
                clientDetail.Middle = client.name.middle;
                clientDetail.Last = client.name.last;
                dbEntities.SaveChanges();
            }

            var clientAddressDetail = (from cd in dbEntities.ClientAddressDetails where cd.ClientId == id select cd).FirstOrDefault();
            if (clientAddressDetail != null)
            {
                clientAddressDetail.Street = client.address.street;
                clientAddressDetail.City = client.address.city;
                clientAddressDetail.State = client.address.state;
                clientAddressDetail.Zip = client.address.zip;
                clientAddressDetail.Email = client.email;
                dbEntities.SaveChanges();
            }

            List<PhoneDetail> clientPhoneDetail = (from cd in dbEntities.PhoneDetails where cd.ClientId == id select cd).ToList();
            if (clientPhoneDetail != null)
            {
                foreach (var newPhone in client.phone)
                {
                    var phoneTypeAlreadyExists = (from phoneRec in clientPhoneDetail where phoneRec.PhoneType.Contains(newPhone.type) select phoneRec).ToList();

                    if (phoneTypeAlreadyExists != null && phoneTypeAlreadyExists.Count > 0)
                    {
                        foreach (var existingPhone in phoneTypeAlreadyExists)
                        {
                            existingPhone.PhoneNumber = newPhone.number;
                            existingPhone.PhoneType = GetPhoneType(newPhone.type);
                            dbEntities.Entry(existingPhone).State = System.Data.Entity.EntityState.Modified;
                            dbEntities.SaveChanges();
                        }
                    }
                    else if (phoneTypeAlreadyExists == null || phoneTypeAlreadyExists.Count == 0)
                    {
                        using (var context = new ContactsRepoEntities())
                        {
                            context.PhoneDetails.Add(
                                                new PhoneDetail
                                                {
                                                    PhoneNumber = newPhone.number,
                                                    PhoneType = newPhone.type,
                                                    ClientId = id
                                                });
                            context.SaveChanges();
                        }
                    }
                }
            }
        }

        // DELETE api/values/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var phoneDetail = (from phone in dbEntities.PhoneDetails where phone.ClientId == id select phone).ToList();
            if (phoneDetail == null || phoneDetail.Count == 0)
            {
                return NotFound();
            }
            else
            {
                foreach (var phn in phoneDetail)
                {
                    dbEntities.Entry(phn).State = System.Data.Entity.EntityState.Deleted;
                    dbEntities.SaveChanges();
                }
            }

            var addressDetail = (from address in dbEntities.ClientAddressDetails where address.ClientId == id select address).FirstOrDefault();
            if (addressDetail == null)
            {
                return NotFound();
            }
            else
            {
                dbEntities.Entry(addressDetail).State = System.Data.Entity.EntityState.Deleted;
                dbEntities.SaveChanges();
            }

            var clientDetail = (from client in dbEntities.ClientDetails where client.Id == id select client).FirstOrDefault();
            if (clientDetail == null)
            {
                return NotFound();
            }
            else
            {
                dbEntities.Entry(clientDetail).State = System.Data.Entity.EntityState.Deleted;
                dbEntities.SaveChanges();
            }

            return Ok();
        }

        private string GetPhoneType(string inputPhoneType)
        {
            string phnType = string.Empty;
            switch (inputPhoneType)
            {
                case "Mobile":
                case "M":
                case "mobile":
                case "MOBILE":
                    phnType = "M";
                    break;
                case "Home":
                case "H":
                case "home":
                case "HOME":
                    phnType = "H";
                    break;
                case "Work":
                case "W":
                case "work":
                case "WORK":
                    phnType = "W";
                    break;
                default:
                    phnType = "M";
                    break;
            }

            return phnType;
        }
    }
}