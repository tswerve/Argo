﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedArrow.Jsorm.Attributes;
using RedArrow.Jsorm.Config;
using RedArrow.Jsorm.JsonModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedArrow.Jsorm.Session
{
    public class Session : IModelSession, ISession
    {
        private HttpClient HttpClient { get; }

        private IDictionary<Type, string> TypeLookup { get; }
        private IDictionary<Type, PropertyInfo> IdLookup { get; }
        private ILookup<Type, PropertyConfiguration> AttributeLookup { get; }

        private SessionState State { get; }

        internal Session(
            Func<HttpClient> httpClientFactory,
            IDictionary<Type, string> typeLookup,
            IDictionary<Type, PropertyInfo> idLookup,
            ILookup<Type, PropertyConfiguration> attributeLookup)
        {
            HttpClient = httpClientFactory();
            TypeLookup = typeLookup;
            IdLookup = idLookup;
            AttributeLookup = attributeLookup;

            State = new SessionState();
        }

        public void Dispose()
        {
            HttpClient.Dispose();
        }

        public async Task<TModel> Create<TModel>(TModel model)
        {
            PropertyInfo idPropInfo;
            if (!IdLookup.TryGetValue(typeof(TModel), out idPropInfo))
            {
                throw new Exception($"{typeof(TModel).FullName} is not a manged model");
            }

            var idVal = idPropInfo.GetValue(model);
            if (!Guid.Empty.Equals(idVal))
            {
                throw new Exception($"Model {typeof(TModel)} [{idVal}] has already been created");
            }

            var id = await CreateRemoteResource(model);
            return CreateModel<TModel>(id);
        }

        private async Task<Guid> CreateRemoteResource<TModel>(TModel model)
        {
	        var modelType = typeof (TModel);
			var resourceType = TypeLookup[modelType];

	        var resourceCreate = new ResourceCreate
	        {
		        Type = resourceType,
		        Attributes = JObject.FromObject(AttributeLookup[modelType]
			        .ToDictionary(
				        x => x.AttributeName,
				        x => x.PropertyInfo.GetValue(model)),
			        JsonSerializer.CreateDefault(new JsonSerializerSettings
			        {
				        NullValueHandling = NullValueHandling.Ignore
			        }))
	        };
			var resourceRequest = new ResourceRootCreate
            {
                Data = resourceCreate
			};

            var body = new StringContent(resourceRequest.ToJson(), Encoding.UTF8, "application/vnd.api+json");
            var response = await HttpClient.PostAsync(resourceType, body);

			response.EnsureSuccessStatusCode(); //TODO

			var locationHeader = response.Headers.Location.ToString();
	        var idStr = locationHeader.Substring(locationHeader.Length - 36, 36);
            var id = Guid.Parse(idStr);

			State.Put(id, new Resource
	        {
		        Id = id,
		        Type = resourceCreate.Type,
		        Attributes = resourceCreate.Attributes
	        });
	        return id;
        }

        public async Task<TModel> Get<TModel>(Guid id)
        {
            //TODO check cache?
            var resource = State.Get(id);
            if (resource == null)
			{
				var resourceType = TypeLookup[typeof(TModel)];
				var response = await HttpClient.GetAsync($"{resourceType}/{id}");
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					return default(TModel); // null
				}
				response.EnsureSuccessStatusCode();
				var contentString = await response.Content.ReadAsStringAsync();
				var resourceRoot = JsonConvert.DeserializeObject<ResourceRootSingle>(contentString);
				State.Put(id, resourceRoot.Data);
			}

            // TODO: cache these so we're not creating a new one on every get
            return CreateModel<TModel>(id);
        }
		
		public Task Delete<TModel>(TModel model)
		{
			PropertyInfo idPropInfo;
			if (!IdLookup.TryGetValue(typeof(TModel), out idPropInfo))
			{
				throw new Exception($"{typeof(TModel).FullName} is not a manged model");
			}

			var idVal = (Guid)idPropInfo.GetValue(model);
			return Delete<TModel>(idVal);
		}

		public async Task Delete<TModel>(Guid id)
		{
			var resourceType = TypeLookup[typeof (TModel)];
			var response = await HttpClient.DeleteAsync($"{resourceType}/{id}");
			response.EnsureSuccessStatusCode();
			State.Evict(id);
		}

        private TModel CreateModel<TModel>(Guid id)
        {
            return (TModel)Activator.CreateInstance(typeof(TModel), id, this);
        }

        public TAttr GetAttribute<TModel, TAttr>(Guid id, string attrName)
        {
            var resource = State.Get(id);
            if (resource == null)
            {
                //TODO: something has gone wrong
                return default(TAttr);
            }

            JToken valueToken;
            if (resource.Attributes.TryGetValue(attrName, out valueToken))
            {
                return valueToken.Value<TAttr>();
            }
            return default(TAttr);
        }

        public void SetAttribute<TModel, TAttr>(Guid id, string attrName, TAttr value)
        {
            //TODO
        }
    }
}