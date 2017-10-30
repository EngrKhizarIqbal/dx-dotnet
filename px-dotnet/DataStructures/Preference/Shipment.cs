﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MercadoPago.Common;

namespace MercadoPago.DataStructures.Preference
{
    public struct Shipment
    {
        #region Properties
        private ShipmentMode? _mode;
        private bool _local_pick_up;
        private string _dimensions;
        private int _default_shipping_method;
        private List<int> _free_methods;
        private float _cost;
        private bool _free_shipping;
        private ReceiverAddress? _receiver_address;  
        #endregion

        #region Accessors
        /// <summary>
        /// Shipment mode
        /// </summary>
        public ShipmentMode? Mode
        { 
            get => _mode; 
            set => _mode = value;
        }
        /// <summary>
        /// The payer have the option to pick up the shipment in your store (mode:me2 only)
        /// </summary>
        public bool LocalPickUp 
        {
            get => _local_pick_up; 
            set => _local_pick_up = value;
        }
        /// <summary>
        /// Dimensions of the shipment in cm x cm x cm, gr (mode:me2 only)
        /// </summary>
        public string Dimensions 
        {
            get => _dimensions; 
            set => _dimensions = value;
        }
        /// <summary>
        /// Select default shipping method in checkout (mode:me2 only)
        /// </summary>
        public int DefaultShippingMethod {
            get => _default_shipping_method; 
            set => _default_shipping_method = value;
        }
        /// <summary>
        /// Offer a shipping method as free shipping (mode:me2 only)
        /// </summary>
        public List<int> FreeMethods {
            get => _free_methods; 
            set => _free_methods = value; 
        }
        /// <summary>
        /// Shipment cost (mode:custom only)
        /// </summary>
        public float Cost {
            get => _cost; 
            set => _cost = value; 
        }
        /// <summary>
        /// Free shipping for mode:custom
        /// </summary>
        public bool FreeShipping {
            get => _free_shipping; 
            set => _free_shipping = value;
        }
        /// <summary>
        /// Shipping address
        /// </summary>
        public ReceiverAddress? ReceiverAddress
        {
            get => _receiver_address;
            set => _receiver_address = value; 
        } 
        #endregion

    }
}