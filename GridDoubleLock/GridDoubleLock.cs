using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class GridDoubleLock : Robot
    {
        #region public param

        [Parameter(DefaultValue = 1000, MinValue = 1000)]
        public int vol { get; set; }

        [Parameter(DefaultValue = 10, MinValue = 1)]
        public int PipSize { get; set; }

        //[Parameter(DefaultValue = 0.1)]
        //public double PipValue1000 { get; set; }

        [Parameter(DefaultValue = 8E-05)]
        public double CommisionAndSpread { get; set; }


        [Parameter(DefaultValue = -1)]
        public double _lifePocket { get; set; }


        //[Parameter(DefaultValue = 5, MinValue = 1)]
        //public double TP { get; set; }

        [Parameter(DefaultValue = 10, MinValue = 1)]
        public int CountPending { get; set; }

        [Parameter(DefaultValue = 3, MinValue = 0)]
        public double MaxPrice { get; set; }

        [Parameter(DefaultValue = 0.1, MinValue = 0)]
        public double MinPrice { get; set; }

        [Parameter(DefaultValue = "dbl")]
        public string CommentSytem { get; set; }

        [Parameter(DefaultValue = true)]
        public bool UseLog { get; set; }

        [Parameter(DefaultValue = 22)]
        public int Timmer { get; set; }


        //[Parameter(DefaultValue = false)]
        //public bool AutoClose { get; set; }

        //[Parameter(DefaultValue = 33, MinValue = 1)]
        //public int PercentOfProfitWillClear { get; set; }

        //[Parameter(DefaultValue = 0, MinValue = 1)]
        //public double StartBalance { get; set; }

        //List<string> _logList = null;

        //double startPrice = 0;

        #endregion

        #region private param

        Dictionary<int, double> lsKeyPrice = new Dictionary<int, double>();
        IDictionary<int, double> lsKeyPriceAbove = null;
        IDictionary<int, double> lsKeyPriceBelow = null;
        //List<int> lsTrade = new List<int>();

        double _maxDDdol = 0;
        bool _haveDup = true;
        //double _totalProfit = 0;

        int _latestPoint = 0;
        int _side = 0;


        //double _lifePocket = 0;
        string PathLog = "D:\\DblGrid";
        int latestPoint = 0;
        bool inPattern = false;
        int pipMultiply = 1;
        bool SL_TP_MinusWay = false;

        bool isCalculatingOnTick = false;
        bool isCalculatingOnOpen = false;
        bool isCalculatingOnClose = false;
        List<string> lsOnOpen = new List<string>();
        List<string> lsOnClose = new List<string>();

        TrendType Buyway = TrendType.None;
        TrendType Sellway = TrendType.None;


        int maxBuyLabel = 0;
        int minSellLabel = 0;
        int lastLabelBuy = 0;
        int lastLabelSell = 0;


        #endregion


        protected override void OnStart()
        {
            try
            {
                Print(">>OnStart");

                Positions.Opened += PositionsOnOpened;
                Positions.Closed += PositionsOnClosed;
                //PendingOrders.Cancelled += PendingOrderOncancel;
                //start timer with second interval
                Timer.Start(Timmer);


                //PathLog += "_" + Account.Number + ".txt";
                //if (_lifePocket < 0 && UseLog)
                //{
                //    GetLastLifePocket();
                //}


                //for use to change price in point -> pip
                for (int i = 0; i < Symbol.Digits - 1; i++)
                {
                    pipMultiply *= 10;
                }


                CalAllPlayPoint();
                NewOpenOrFixInLength();

                Print("-----OnStart----");
            } catch (Exception ex)
            {
                Print("Error OnStart >> " + ex.Message);
            }
        }

        protected override void OnTick()
        {
            if (!isCalculatingOnTick)
            {
                isCalculatingOnTick = true;

                Task.Factory.StartNew(() => DrawTextOnGraph()).Wait();

                isCalculatingOnTick = false;
            }
        }

        protected override void OnTimer()
        {
            //Task tFix = Task.Factory.StartNew(() => NewOpenOrFixInLength());
            //Task tCleanDup = Task.Factory.StartNew(() => CleanDupOrderAndPending());
            //tFix.Wait();
            //tCleanDup.Wait();
        }

        protected override void OnStop()
        {
            if (UseLog)
            {
                UpdateLifePocket();
            }
            Print("Opened Order Count=" + Positions.Count);
            Print("pending Order Count=" + PendingOrders.Count);
            Print("MaxDDdol=" + _maxDDdol);
        }

        //protected override void OnBar()
        //{
        //    //NewOpenOrFixInLength();

        //    //_haveDup = true;
        //    //while (_haveDup)
        //    //{
        //    //    CleanDupOrderAndPending();
        //    //}



        //    //Print("Positions>> " + Positions.Count);
        //    //Print("PendingOrders>> " + PendingOrders.Count);

        //    //string sAcc = "3172512";
        //    //if (Account.Number.ToString() != sAcc)
        //    //    Stop();

        //    //if (Account.IsLive)
        //    //    Stop();


        //    //List<string> lsBroke = new List<string> 
        //    //{
        //    //    "IC MARKETS",
        //    //    "OCTAFX",
        //    //    "PEPPERSTONE"
        //    //};
        //    //if (!lsBroke.Contains(Account.BrokerName.ToUpper()))
        //    //    Stop();

        //    //double d = Account.Balance - Account.Equity;
        //    //if (d > _maxDDdol)
        //    //{
        //    //    _maxDDdol = d;
        //    //}

        //}

        //protected override void OnTimer()
        //{
        //    //haveDup = true;
        //    //while (haveDup)
        //    //{
        //    //    CleanDupOrderAndPending();
        //    //}

        //    //NewOpenOrFixInLength();
        //}

        private void PositionsOnOpened(PositionOpenedEventArgs args)
        {
            try
            {
                var poOpen = args.Position;
                int poLabel = Convert.ToInt32(poOpen.Label);


                if (lastLabelBuy != 0 || lastLabelSell != 0)
                {
                    if (poOpen.TradeType == TradeType.Buy)
                    {
                        //ทำ new hi
                        if (poLabel > maxBuyLabel)
                        {
                            lastLabelBuy = Convert.ToInt32(poOpen.Label);
                            maxBuyLabel = Convert.ToInt32(poOpen.Label);
                            Task.Factory.StartNew(() => ClearPending(TradeType.Sell, poOpen.Label, true)).Wait();
                            Task.Factory.StartNew(() => ReInitialPendingSellWhileUp(poLabel)).Wait();
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        //ทำ new low
                        if (lastLabelSell != 0)
                        {
                            if (Convert.ToInt32(poOpen.Label) < minSellLabel)
                            {
                                lastLabelSell = Convert.ToInt32(poOpen.Label);
                                minSellLabel = Convert.ToInt32(poOpen.Label);
                                Task.Factory.StartNew(() => ClearPending(TradeType.Buy, poOpen.Label, true)).Wait();
                                Task.Factory.StartNew(() => ReInitialPendingBuyWhileDown(poLabel)).Wait();
                            }
                            else
                            {

                            }
                        }
                    }
                }
                else
                {
                    if (poOpen.TradeType == TradeType.Buy)
                    {
                        lastLabelSell = Convert.ToInt32(poOpen.Label);
                        minSellLabel = Convert.ToInt32(poOpen.Label);

                        Task.Factory.StartNew(() => ClearPending(TradeType.Sell, poOpen.Label, true)).Wait();

                        Task.Factory.StartNew(() => ReInitialPendingSellWhileUp(poLabel)).Wait();


                    }
                    else
                    {
                        lastLabelSell = Convert.ToInt32(poOpen.Label);
                        minSellLabel = Convert.ToInt32(poOpen.Label);

                        Task.Factory.StartNew(() => ClearPending(TradeType.Buy, poOpen.Label, true)).Wait();

                        Task.Factory.StartNew(() => ReInitialPendingBuyWhileDown(poLabel)).Wait();

                    }
                }

                //Print(">>PositionsOnOpened");

                //var poOpen = args.Position;

                //if (isCalculatingOnOpen)
                //{
                //    lsOnOpen.Add(poOpen.Label);
                //    return;
                //}
                //isCalculatingOnOpen = true;


                ////Task tPlusBuy = Task.Factory.StartNew(() => CalTP_SL_PlusWay_BuyWay_OnOpened());
                ////Task tPlusSell = Task.Factory.StartNew(() => CalTP_SL_PlusWay_SellWay_OnOpened());
                ////tPlusBuy.Wait();
                ////tPlusSell.Wait();



                ////ถ้ามีไม้เปิดมาแทรก ทำแค่อีกครั้งเดียวก็พอ
                //int countPoOpened = lsOnOpen.Count;
                //while (countPoOpened > 0)
                //{


                //    for (int i = 0; i < countPoOpened; i++)
                //    {
                //        lsOnOpen.RemoveAt(0);
                //    }
                //    //เช็คเผื่อไม่เป็น 0 มีแทรกมาอีก -*-
                //    countPoOpened = lsOnOpen.Count;
                //}
                //isCalculatingOnOpen = false;


                //Print("-----PositionsOnOpened-----");
            } catch (Exception ex)
            {
                Print("Error PositionsOnOpened >> " + ex.Message);
            }
        }

        private void PositionsOnClosed(PositionClosedEventArgs args)
        {
            try
            {
                var po = args.Position;

                Task.Factory.StartNew(() => FixAfterClose(po)).Wait();

                //if (isCalculatingOnClose)
                //{
                //    lsOnClose.Add(po.Label);
                //    return;
                //}

                //Task.Factory.StartNew(() => FixAfterClose(po)).Wait();

                //while (lsOnClose.Count > 0)
                //{
                //    //Task.Factory.StartNew(() => CheckClose(po.Label)).Wait();
                //    lsOnClose.RemoveAt(0);
                //}

                ////Task.Factory.StartNew(() => FixAfterClose(po)).Wait();
                ////NewOpenOrFixInLength();
                ////CleanDupOrderAndPending();

                //isCalculatingOnOpen = false;

            } catch (Exception ex)
            {
                Print("Error PositionsOnClosed >> " + ex.Message);
            }
        }

        private void ClearTP_SLOrder(TradeType traderType)
        {
            try
            {
                Print(">>ClearTP_SLOrder");

                foreach (var po in Positions.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.TradeType == traderType))
                {
                    ModifyPositionAsync(po, null, null, null);
                }

                Print("-----ClearTP_SLOrder-----");
            } catch (Exception ex)
            {
                Print("Error CalTP_SL_MinusWay >> " + ex.Message);
            }
        }

        private void CalAllPlayPoint()
        {
            lsKeyPrice = new Dictionary<int, double>();

            double dPrice = MinPrice;
            int index = 1;
            while (dPrice < MaxPrice)
            {
                lsKeyPrice.Add(index, dPrice);
                dPrice += Convert.ToDouble((PipSize * 0.0001).ToString("0.00000"));
                index++;
            }

            //Print("--------------------------------------------------------------");
            //foreach (var a in lsPrice)
            //{
            //    Print("Key>> " + a.Key + " value>> " + a.Value.ToString("0.00000"));
            //}
            //Print("--------------------------------------------------------------");
            //Stop();
        }

        private void NewOpenOrFixInLength()
        {
            try
            {
                Print(">>NewOpenOrFixInLength");

                //มีออเดอร์ค้างไหม คือ ไม่่มีเข้าลูปเริ่มต้นวางไม้อย่างง่ายๆเลย ปล.ต้องไม่นับรวมไม้จากสูตรอื่น
                if (Positions.Count(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem) == 0)
                {
                    InitialPendingOrder();
                }
                else
                {
                    //Task fix = Task.Factory.StartNew(() => FixInPendingLength());
                    //Task clean = Task.Factory.StartNew(() => CleanDupOrderAndPending());

                    //Task tPlusBuy = Task.Factory.StartNew(() => CalTP_SL_PlusWay_BuyWay_OnOpened());
                    //Task tPlusSell = Task.Factory.StartNew(() => CalTP_SL_PlusWay_SellWay_OnOpened());
                    //tPlusBuy.Wait();
                    //tPlusSell.Wait();

                    //Cal_TpSl_MinusWay(TradeType.Buy);
                    //Cal_TpSl_MinusWay(TradeType.Sell);

                    //fix.Wait();
                    //clean.Wait();
                }

                Print("-----NewOpenOrFixInLength-----");
            } catch (Exception ex)
            {
                Print("Error NewOpenOrFixInLength >> " + ex.Message);
            }
        }

        private void InitialPendingOrder()
        {
            var lsKPLess = lsKeyPrice.Where(a => a.Value <= Symbol.Ask).OrderByDescending(o => o.Key).Take(CountPending).ToList();
            var lsKPMore = lsKeyPrice.Where(a => a.Value >= Symbol.Ask).OrderBy(o => o.Key).Take(CountPending).ToList();


            //เปิด pending ที่ราคาสูงกว่าปัจจุบัน
            int n = 1;
            foreach (var kp in lsKPMore)
            {
                PlaceStopOrderAsync(TradeType.Buy, Symbol, CalNFlag(n), kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                PlaceLimitOrderAsync(TradeType.Sell, Symbol, vol, kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                n++;
            }

            //เปิด pending ที่ราคาต่ำกว่าปัจจุบัน
            n = 1;
            foreach (var kp in lsKPLess)
            {
                PlaceLimitOrderAsync(TradeType.Buy, Symbol, vol, kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                PlaceStopOrderAsync(TradeType.Sell, Symbol, CalNFlag(n), kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                n++;
            }
        }

        private void ReInitialPendingOrder(TradeType tradeType, int Openedlabel)
        {
            if (tradeType == TradeType.Buy)
            {
                var lsKPMore = lsKeyPrice.Where(a => a.Key > Openedlabel).OrderBy(o => o.Key).Take(CountPending).ToList();

                //เปิด pending ที่ราคาสูงกว่าปัจจุบัน เฉพาะขา
                int n = 2;
                foreach (var kp in lsKPMore)
                {
                    PlaceStopOrderAsync(TradeType.Buy, Symbol, CalNFlag(n), kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                    //PlaceLimitOrderAsync(TradeType.Sell, Symbol, vol, kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                    n++;
                }
            }
            else
            {
                var lsKPLess = lsKeyPrice.Where(a => a.Value < Openedlabel).OrderByDescending(o => o.Key).Take(CountPending).ToList();

                //เปิด pending ที่ราคาต่ำกว่าปัจจุบัน เฉพาะขา
                int n = 2;
                foreach (var kp in lsKPLess)
                {
                    //PlaceLimitOrderAsync(TradeType.Buy, Symbol, vol, kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                    PlaceStopOrderAsync(TradeType.Sell, Symbol, CalNFlag(n), kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                    n++;
                }
            }




        }

        List<string> cancelPedingList = new List<string>();
        private void ClearPending(TradeType tradeType, string presentLabel, bool isWait)
        {
            var pes = PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.TradeType == tradeType && x.Label != presentLabel);
            foreach (var pe in pes)
            {
                cancelPedingList.Add(pe.Label);
                CancelPendingOrderAsync(pe, CancelPendingCallback);
            }

            int countWait = 0;
            while (cancelPedingList.Count > 0 && isWait && countWait < 10)
            {
                Print("รอกำลังยกเลิก pending ขา>>" + tradeType + " เหลือ>>" + cancelPedingList.Count);
                System.Threading.Thread.Sleep(1000);
            }
            Print("ครบ10วินาที pending ขา>>" + tradeType + " เหลือ>>" + cancelPedingList.Count);
            cancelPedingList = new List<string>();
        }
        private void CancelPendingCallback(TradeResult tr)
        {
            if (tr.IsSuccessful)
            {
                cancelPedingList.Remove(tr.PendingOrder.Label);
            }
            else
            {
                var tr2 = CancelPendingOrder(tr.PendingOrder);
                if (tr2.IsSuccessful)
                {
                    cancelPedingList.Remove(tr.PendingOrder.Label);
                }
                else
                {
                    if (PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Label == tr.PendingOrder.Label).Count() > 0)
                    {
                        Print("ERROR!!! ไม่สามารถยกเลิก pending >>>" + tr.PendingOrder.Label);
                        cancelPedingList.Remove(tr.PendingOrder.Label);
                    }
                    else
                    {
                        cancelPedingList.Remove(tr.PendingOrder.Label);
                    }
                }
            }
        }

        private void ReInitialPendingBuyWhileDown(int poLabel)
        {
            var lsKPLess = lsKeyPrice.Where(a => a.Key >= poLabel).OrderByDescending(o => o.Key).Take(CountPending).ToList();

            //เปิด pending ที่ราคาต่ำกว่าปัจจุบัน
            int n = 2;
            foreach (var kp in lsKPLess)
            {
                PlaceLimitOrderAsync(TradeType.Buy, Symbol, CalNFlag(n), kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                //PlaceStopOrderAsync(TradeType.Sell, Symbol, CalNFlag(n), kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                n++;
            }
        }
        private void ReInitialPendingSellWhileUp(int poLabel)
        {
            var lsKPLess = lsKeyPrice.Where(a => a.Key <= poLabel).OrderByDescending(o => o.Key).Take(CountPending).ToList();

            //เปิด pending ที่ราคาต่ำกว่าปัจจุบัน
            int n = 2;
            foreach (var kp in lsKPLess)
            {
                //PlaceLimitOrderAsync(TradeType.Buy, Symbol, vol, kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                PlaceStopOrderAsync(TradeType.Sell, Symbol, CalNFlag(n), kp.Value, kp.Key.ToString(), null, (PipSize), null, CommentSytem, null);
                n++;
            }
        }


        private int CalNFlag(int n)
        {
            int result = ((n * (n - 1)) + 1) * vol;
            if (result == 0)
            {
                return 1000;
            }
            else
            {
                return result;
            }
        }

        private void FixInPendingLength()
        {
            try
            {
                Print(">>FixInPendingLength");


                var lsKPMore = lsKeyPrice.Where(a => a.Value > Symbol.Ask).OrderBy(o => o.Key).Take(CountPending).Select(x => x.Key).Max();




                //var lsKPMore = lsKeyPrice.Where(a => a.Value > Symbol.Ask).OrderBy(o => o.Key).Take(CountPending);
                //var lsKPLess = lsKeyPrice.Where(a => a.Value < Symbol.Bid).OrderByDescending(o => o.Key).Take(CountPending);

                #region ซ่อมรายการที่ขาด


                ////เปิด pending ที่ราคาสูงกว่าปัจจุบัน
                //foreach (var kp in lsKPMore)
                //{
                //    //เช็คว่ารายการขา buy ยังอยู่ครบไหม ดูในpendingก่อนเพราะเปอร์เซ็นรายการเยอะกว่า
                //    int iPeB = PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Buy).Count();
                //    int iPoB = Positions.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Buy).Count();
                //    if (iPeB == 0 && iPoB == 0)
                //    {
                //        //เปิด buy ที่สูงกว่าปัจจุบันคือ BuyStop
                //        Print("กำลังวาง pending ขา>>buy label>>" + kp.Key);
                //        PlaceStopOrderAsync(TradeType.Buy, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem, null);
                //    }

                //    //เช็คว่ารายการขา Sell ยังอยู่ครบไหม ดูในpendingก่อนเพราะเปอร์เซ็นรายการเยอะกว่า
                //    int iPeS = PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Sell).Count();
                //    int iPoS = Positions.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Sell).Count();
                //    if (iPeB == 0 && iPoB == 0)
                //    {
                //        //เปิด sell ที่สูงกว่าปัจจุบันคือ SellLimit
                //        Print("กำลังวาง pending ขา>>sell label>>" + kp.Key);
                //        PlaceLimitOrderAsync(TradeType.Sell, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem, null);
                //    }
                //}

                ////เปิด pending ที่ราคาต่ำกว่าปัจจุบัน
                //foreach (var kp in lsKPLess)
                //{
                //    //เช็คว่ารายการขา buy ยังอยู่ครบไหม ดูในpendingก่อนเพราะเปอร์เซ็นรายการเยอะกว่า
                //    if (PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Buy).Count() == 0)
                //    {
                //        if (Positions.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Buy).Count() == 0)
                //        {
                //            //เปิด buy ที่สูงกว่าปัจจุบันคือ BuyLimit
                //            Print("กำลังวาง pending ขา>>buy label>>" + kp.Key);
                //            PlaceLimitOrderAsync(TradeType.Buy, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem, null);
                //        }
                //    }

                //    //เช็คว่ารายการขา Sell ยังอยู่ครบไหม ดูในpendingก่อนเพราะเปอร์เซ็นรายการเยอะกว่า
                //    if (PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Sell).Count() == 0)
                //    {
                //        if (Positions.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Sell).Count() == 0)
                //        {
                //            //เปิด sell ที่สูงกว่าปัจจุบันคือ SellLimit
                //            Print("กำลังวาง pending ขา>>sell label>>" + kp.Key);
                //            PlaceStopOrderAsync(TradeType.Sell, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem, null);
                //        }
                //    }
                //}



                #endregion

                #region ปิดรายการที่เกิน



                #endregion

                Print("-----FixInPendingLength-----");
            } catch (Exception ex)
            {
                Print("Error FixInPendingLength >> " + ex.Message);
            }

        }

        private void FixAfterClose(Position po)
        {
            try
            {
                Print(">>FixAfterClose");

                ////PlaceStopOrderAsync(po.TradeType, Symbol, vol, lsKeyPrice[Convert.ToInt32(po.Label)], po.Label, null, PipSize, null, CommentSytem);
                ////PlaceLimitOrderAsync(po.TradeType, Symbol, vol, lsKeyPrice[Convert.ToInt32(po.Label)], po.Label, null, PipSize, null, CommentSytem);
                ///
                if (po.TradeType == TradeType.Buy)
                {
                    PlaceLimitOrder(TradeType.Buy, Symbol, vol, lsKeyPrice[Convert.ToInt32(po.Label)], po.Label, null, PipSize, null, CommentSytem);
                }
                else
                {
                    PlaceStopOrder(TradeType.Sell, Symbol, vol, lsKeyPrice[Convert.ToInt32(po.Label)], po.Label, null, PipSize, null, CommentSytem);
                }


                //var lsKPMore = lsKeyPrice.Where(a => a.Value > Symbol.Ask).OrderBy(o => o.Key).Take(CountPending);
                //var lsKPLess = lsKeyPrice.Where(a => a.Value < Symbol.Bid).OrderByDescending(o => o.Key).Take(CountPending);

                ////เปิด pending ที่ราคาสูงกว่าปัจจุบัน
                //foreach (var kp in lsKPMore.Where(x => x.Key == Convert.ToInt32(po.Label)))
                //{
                ////if (po.TradeType == TradeType.Buy)
                ////{
                ////    if (PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Buy).Count() == 0)
                ////    {
                ////        PlaceStopOrder(TradeType.Buy, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem);
                ////    }
                ////}
                ////else
                ////{
                ////    if (PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Sell).Count() == 0)
                ////    {
                ////        PlaceLimitOrder(TradeType.Sell, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem);
                ////    }
                ////}
                //}

                ////เปิด pending ที่ราคาต่ำกว่าปัจจุบัน
                //foreach (var kp in lsKPLess.Where(x => x.Key == Convert.ToInt32(po.Label)))
                //{
                //    if (po.TradeType == TradeType.Buy)
                //    {
                //        if (PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Buy).Count() == 0)
                //        {
                //            PlaceLimitOrder(TradeType.Buy, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem);
                //        }
                //    }
                //    else
                //    {
                //        if (PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem && x.Label == kp.Key.ToString() && x.TradeType == TradeType.Sell).Count() == 0)
                //        {
                //            PlaceStopOrder(TradeType.Sell, Symbol, vol, kp.Value, kp.Key.ToString(), null, null, null, CommentSytem);
                //        }
                //    }
                //}



                Print("-----FixAfterClose-----");
            } catch (Exception ex)
            {
                Print("Error FixAfterClose >> " + ex.Message);
            }
        }

        private void CleanDupOrderAndPending()
        {
            try
            {
                //Print(">>>CleanDupOrderAndPending<<<<");

                var pes = PendingOrders.GroupBy(s => s).SelectMany(grp => grp.Skip(0));
                foreach (var pe in pes)
                {
                    NotiSound();
                    Print(pe.Label + " " + pe.TradeType + pe.OrderType);
                    //CancelPendingOrderAsync(pe);
                }

                var pos = Positions.GroupBy(s => s).SelectMany(grp => grp.Skip(0));
                foreach (var po in pos)
                {
                    NotiSound();
                    Print("มี PO เกิน>>" + po.Label + " " + po.TradeType);
                    //ClosePositionAsync(po);
                }

                //foreach (var po in Positions.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem))
                //{
                //    var countPo = Positions.Where(x => x.SymbolCode == Symbol.Code && x.Label.ToLower() == po.Label.ToLower() && x.TradeType == po.TradeType).Count();

                //    //มี po เกินก็เคลียก่อนเลย
                //    if (countPo > 1)
                //    {
                //        Print("มี position เกิน คือ " + po.Label);
                //        //ClosePosition(po);
                //    }
                //}

                //foreach (var pe in PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment == CommentSytem))
                //{
                //    var Pos = Positions.Where(x => x.SymbolCode == Symbol.Code && x.Label == pe.Label && x.TradeType == pe.TradeType);
                //    var Pes = PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Label == pe.Label && x.TradeType == pe.TradeType);

                //    if (Pes.Count() > 1)
                //    {
                //        Print("มี pending เกิน คือ " + pe.Label);
                //        //Notifications.PlaySound("C:\\Windows\\Media\\Alarm10.wav");
                //        CancelPendingOrder(pe);
                //    }

                //    ////มี po เกินก็เคลียก่อนเลย
                //    //if (Pes.Count() == 1)
                //    //{
                //    //    //มี po แล้ว ก็เคลีย pe นั้นทิ้ง
                //    //    if (Pos.Count() >= 1)
                //    //    {
                //    //        //Print("มี pending เกิน คือ " + pe.Label);
                //    //        CancelPendingOrder(pe);
                //    //    }
                //    //}
                //}

                ////ปิดpending ที่ไม่อยู่ในระยะปัจจุบัน
                //var lsLess = lsPrice.Where(a => a.Value <= Symbol.Ask).OrderByDescending(o => o.Key).Take(CountPending).ToList();
                //var lsMore = lsPrice.Where(a => a.Value >= Symbol.Ask).OrderBy(o => o.Key).Take(CountPending).ToList();
                //List<int> lsTrade = new List<int>();
                //foreach (var kk in lsLess.OrderBy(o => o.Key))
                //{
                //    lsTrade.Add(kk.Key);
                //}
                //foreach (var kk in lsMore.OrderBy(o => o.Key))
                //{
                //    lsTrade.Add(kk.Key);
                //}

                ////หา pending ที่ถูกตั้งที่ไม่อยู่ในระยะ
                //foreach (var pe in PendingOrders.Where(x => x.SymbolCode == Symbol.Code && x.Comment.ToLower() == "cb"))
                //{
                //    if (!lsTrade.Contains(Convert.ToInt32(pe.Label)))
                //    {
                //        //Print("มี pending เกินหลุดขอบ คือ " + pe.Label);
                //        CancelPendingOrder(pe);
                //    }
                //}

                ////ถ้าไม่มีอะไรซ้ำก็อัพเดต haveDup ว่าไม่มีแล้ว ก็จะหลุด lopp while
                //haveDup = false;

                //Print("------------CleanDupOrderAndPending-------------");

            } catch (Exception ex)
            {
                Print("Error in CleanDupOrderAndPending.");
                Print(ex.Message);
            }

        }


        private void NotiSound()
        {
            Notifications.PlaySound("C:\\Windows\\Media\\Alarm10.wav");
        }
        public void SendLine(string msg)
        {
            //Print(msg);
            string Bearer = "09cuycGtLAqfYpibhABtUEPRwf9BcmLuV9l0tb4qqTl";
            var request = (HttpWebRequest)WebRequest.Create("https://notify-api.line.me/api/notify");
            var postData = string.Format("message={0}", msg);
            var data = Encoding.UTF8.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;
            request.Headers.Add("Authorization", "Bearer " + Bearer);

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();
            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

            Print(responseString);
        }
        private void DrawTextOnGraph()
        {
            List<string> lsN = new List<string>();
            ChartObjects.DrawText("maxBuyLabel >> ", "   maxBuyLabel:" + maxBuyLabel, StaticPosition.TopLeft, Colors.LightBlue);

            lsN.Add("\n");
            ChartObjects.DrawText("minSellLabel", string.Join("", lsN) + "          minSellLabel:" + minSellLabel, StaticPosition.TopLeft, Colors.LightBlue);

            lsN.Add("\n");
            ChartObjects.DrawText("lastLabelBuy", string.Join("", lsN) + "          lastLabelBuy:" + lastLabelBuy, StaticPosition.TopLeft, Colors.LightBlue);

            lsN.Add("\n");
            ChartObjects.DrawText("lastLabelSell", string.Join("", lsN) + "          lastLabelSell:" + lastLabelSell, StaticPosition.TopLeft, Colors.LightBlue);

            //string sProfitBuy = "";
            //foreach (var ls in lsWillProfitClearBuy)
            //{
            //    sProfitBuy += ls.Key + ":" + ls.Value + "  |  ";
            //}
            //lsN.Add("\n");
            //ChartObjects.DrawText("lsWillProfitClearBuy", string.Join("", lsN) + "          รายการกำไรขาbuy:" + sProfitBuy, StaticPosition.TopLeft, Colors.LightBlue);

            //string sProfitSell = "";
            //foreach (var ls in lsWillProfitClearSell)
            //{
            //    sProfitSell += ls.Key + ":" + ls.Value + "  |  ";
            //}
            //lsN.Add("\n");
            //ChartObjects.DrawText("lsWillProfitClearSell", string.Join("", lsN) + "          รายการกำไรขาsell:" + sProfitSell, StaticPosition.TopLeft, Colors.LightBlue);

            //string sLossBuy = "";
            //foreach (var ls in lsWillLossClearBuy)
            //{
            //    sLossBuy += ls.Key + ":" + ls.Value + "  |  ";
            //}
            //lsN.Add("\n");
            //ChartObjects.DrawText("lsWillLossClearBuy", string.Join("", lsN) + "          รายการขาดทุนขาbuy:" + sLossBuy, StaticPosition.TopLeft, Colors.LightBlue);

            //string sLossSell = "";
            //foreach (var ls in lsWillLossClearSell)
            //{
            //    sLossSell += ls.Key + ":" + ls.Value + "  |  ";
            //}
            //lsN.Add("\n");
            //ChartObjects.DrawText("lsWillLossClearSell", string.Join("", lsN) + "          รายการขาดทุนขาsell:" + sLossSell, StaticPosition.TopLeft, Colors.LightBlue);


            //foreach (var ls in lsOnOpen)
            //{
            //    lsN.Add("\n");
            //    ChartObjects.DrawText("lsOnOpen", string.Join("", lsN) + "          รายการรอคิวเปิด:" + ls, StaticPosition.TopLeft, Colors.LightBlue);
            //}


        }
        private void GetLastLifePocket()
        {
            try
            {
                //var f = new FileInfo(PathLog);
                //var fs = f.Create();

                //// you can use dispose here, for it returns filestream
                //fs.Dispose();

                //FileStream fs = new FileStream(PathLog, FileMode.OpenOrCreate);
                //fs.re
                //fs.Dispose();

                FileInfo fInfo = new FileInfo(PathLog);
                if (!fInfo.Exists)
                {
                    fInfo = null;
                    File.Create(PathLog);
                }

                var _logList = new List<string>(File.ReadAllLines(PathLog));
                if (_logList.Count > 0)
                {
                    _lifePocket = Convert.ToDouble(_logList.Last());
                    Print("lifePocket >> " + _lifePocket);
                }
                else
                {
                    _lifePocket = 0;
                    Print("lifePocket >> " + _lifePocket);
                }
            } catch (Exception ex)
            {
                _lifePocket = 0;

                Print("ERROR GET LOG FILE!");
                Print(ex.Message);
                Stop();
            }
        }
        private void UpdateLifePocket()
        {
            try
            {
                if (UseLog)
                    File.WriteAllText(PathLog, _lifePocket.ToString("0.00"));
            } catch (Exception ex)
            {
                Print("Error UpdateLifePocket >> " + ex.Message);
                Stop();
            }
        }


    }

    public enum TrendType
    {
        Up = 1,
        None = 0,
        Down = -1
    }
}
