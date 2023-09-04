using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcrConnect.HomePage.Api.Contract;
using AcrConnect.HomePage.ApplicationCallbacks.Data.Models;
using AcrConnect.HomePage.Data;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static System.FormattableString;

namespace AcrConnect.HomePage.ApplicationCallbacks
{
    public sealed class CallbacksService
    {
        #region Fields

        [NotNull] private readonly ApplicationDbContext _context;
        [NotNull] private readonly ILogger<CallbacksService> _logger;

        #endregion Fields

        #region Constructors

        public CallbacksService([NotNull] ApplicationDbContext context,
            [NotNull] ILogger<CallbacksService> logger)
        {
            Assert.NotNull(context, nameof(context));
            Assert.NotNull(logger, nameof(logger));
            _context = context;
            _logger = logger;
        }

        #endregion Constructors

        #region Public Methods

        [NotNull]
        public async Task AddOrUpdateAsync([NotNull] string applicationKey, [NotNull] ApiCallback callback, CancellationToken cancellationToken)
        {
            Assert.NotNull(applicationKey, nameof(applicationKey));
            Assert.NotNull(callback, nameof(callback));

            var entity = await FindCallbackAsync(callback.PublicId, cancellationToken).ConfigureAwait(false);
            if (entity == null)
            {
                entity = new ApiCallbackEntity
                         {
                             PublicId = callback.PublicId,
                             ApplicationKey = applicationKey,
                             ApiType = callback.ApiType,
                             ApiVersion = callback.ApiVersion,
                             RelativeCallbackUri = callback.RelativeCallbackUri
                         };
                await _context.Set<ApiCallbackEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                entity.ApiType = callback.ApiType;
                entity.ApiVersion = callback.ApiVersion;
                entity.RelativeCallbackUri = callback.RelativeCallbackUri;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving ApiCallbackEntity to database");
                throw;
            }
        }

        [NotNull]
        public async Task RemoveAsync(Guid callbackId, CancellationToken cancellationToken)
        {
            var entity = await FindCallbackAsync(callbackId, cancellationToken).ConfigureAwait(false);
            if (entity == null)
            {
                return;
            }

            try
            {
                _context.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error while performing database operation to ApiCallbackEntity");
                 throw;
            }
        }

        [NotNull, ItemNotNull]
        public async Task<ApiCallback[]> GetCallbacksAsync([NotNull] string applicationKey, CancellationToken cancellationToken)
        {
            Assert.NotNull(applicationKey, nameof(applicationKey));

            return await _context.Set<ApiCallbackEntity>()
                .TagWith(Invariant($"Search for all existing callbacks for application '{applicationKey}'"))
                .Where(x => x.ApplicationKey == applicationKey)
                .Select(x => new ApiCallback
                             {
                                 PublicId = x.PublicId,
                                 ApiType = x.ApiType,
                                 ApiVersion = x.ApiVersion,
                                 RelativeCallbackUri = x.RelativeCallbackUri
                             })
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        #endregion Public Methods

        #region Helpers

        [NotNull, ItemCanBeNull]
        private async Task<ApiCallbackEntity> FindCallbackAsync(Guid callbackId, CancellationToken cancellationToken)
        {
            return await _context.Set<ApiCallbackEntity>()
                .TagWith(Invariant($"Search for existing callback #{callbackId}"))
                .FirstOrDefaultAsync(x => x.PublicId == callbackId, cancellationToken)
                .ConfigureAwait(false);
        }

        #endregion Helpers
    }
}
