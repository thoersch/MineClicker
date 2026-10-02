#if IDLEMINE_UNITY_IAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace IdleMine
{
    /// <summary>
    /// Google Play / App Store purchases through Unity IAP 5 (package com.unity.purchasing, built against
    /// 5.4.3). Compiled only when the IDLEMINE_UNITY_IAP scripting define is set. Product ids must match
    /// the ones created in Play Console and App Store Connect.
    ///
    /// IAP 5 purchases are two-step: the store reports a pending order, we grant the entitlement, then
    /// confirm it. Owned non-consumables come back through the same path at startup and on restore.
    /// </summary>
    public class UnityIapPurchaseService : IPurchaseService
    {
        StoreController _store;
        readonly List<ProductDefinition> _products = new List<ProductDefinition>();
        bool _connected;
        string _pendingId;
        Action<bool> _pending;

        public event Action<string> Owned;

        public bool IsReady { get { return _connected; } }

        public async void Initialize(string[] nonConsumableIds)
        {
            foreach (var id in nonConsumableIds) _products.Add(new ProductDefinition(id, ProductType.NonConsumable));

            _store = UnityIAPServices.StoreController();
            // Every event is subscribed before Connect: pending orders from a previous session can arrive immediately.
            _store.OnStoreConnected += OnConnected;
            _store.OnStoreDisconnected += failure => { _connected = false; Debug.LogWarning("[IdleMine] Store disconnected: " + failure.Message); };
            _store.OnProductsFetched += products => { };
            _store.OnProductsFetchFailed += failure => Debug.LogWarning("[IdleMine] Product fetch failed: " + failure.FailureReason);
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += failure => Debug.LogWarning("[IdleMine] Purchase fetch failed: " + failure.Message);
            _store.OnPurchasePending += OnPending;
            _store.OnPurchaseConfirmed += OnConfirmed;
            _store.OnPurchaseFailed += OnFailed;
            _store.OnPurchaseDeferred += order =>
            {
                // Ask to Buy / slow payment: nothing to grant until it's approved and comes back as pending.
                Debug.Log("[IdleMine] Purchase deferred until approved.");
                FinishPending(ProductId(order), false);
            };

            try { await _store.Connect(); }
            catch (Exception e) { Debug.LogWarning("[IdleMine] Store connect failed: " + e.Message); }
        }

        void OnConnected()
        {
            _connected = true;
            _store.FetchProducts(_products);
            _store.FetchPurchases();
        }

        public string Price(string productId)
        {
            if (_store == null) return null;
            var p = _store.GetProductById(productId);
            return p != null && p.availableToPurchase ? p.metadata.localizedPriceString : null;
        }

        public void Purchase(string productId, Action<bool> done)
        {
            var product = _store != null ? _store.GetProductById(productId) : null;
            if (!_connected || product == null || _pending != null) { done(false); return; }
            _pendingId = productId;
            _pending = done;
            _store.PurchaseProduct(product);
        }

        public void Restore(Action<bool> done)
        {
            if (!_connected) { done(false); return; }
            // Each restored purchase arrives through OnPurchasePending, which grants it.
            _store.RestoreTransactions((ok, error) =>
            {
                if (!ok) Debug.LogWarning("[IdleMine] Restore failed: " + error);
                done(ok);
            });
        }

        // ================================================================== store events

        void OnPurchasesFetched(Orders orders)
        {
            foreach (var order in orders.ConfirmedOrders) RaiseOwned(ProductId(order));
        }

        void OnPending(PendingOrder order)
        {
            // Grant first, then confirm. Unconfirmed orders are re-delivered on the next launch, and granting
            // the pass twice is harmless, so nothing paid for can be lost.
            string id = ProductId(order);
            RaiseOwned(id);
            _store.ConfirmPurchase(order);
            FinishPending(id, true);
        }

        void OnConfirmed(Order order)
        {
            var failed = order as FailedOrder;
            if (failed != null) Debug.LogWarning("[IdleMine] Purchase confirmation failed: " + failed.FailureReason + " " + failed.Details);
        }

        void OnFailed(FailedOrder order)
        {
            string id = ProductId(order);
            Debug.Log("[IdleMine] Purchase failed: " + order.FailureReason + " " + order.Details);
            // Already owned on this account: treat it as a restore.
            bool owned = order.FailureReason == PurchaseFailureReason.DuplicateTransaction;
            if (owned) RaiseOwned(id);
            FinishPending(id, owned);
        }

        static string ProductId(Order order)
        {
            foreach (var item in order.CartOrdered.Items()) return item.Product.definition.id;
            return null;
        }

        void RaiseOwned(string id)
        {
            if (id != null && Owned != null) Owned(id);
        }

        void FinishPending(string id, bool ok)
        {
            if (_pending == null || (id != null && id != _pendingId)) return;
            var done = _pending;
            _pending = null;
            _pendingId = null;
            done(ok);
        }
    }
}
#endif
