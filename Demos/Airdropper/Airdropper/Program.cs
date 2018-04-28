﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Neo.Lux.Cryptography;
using Neo.Lux.Core;
using Neo.Lux.Utils;

namespace Neo.Lux.Airdropper
{
    class AirDropper
    {
        static void Main()
        {
            var api = NeoDB.ForMainNet();

            api.SetLogger(x => Console.WriteLine(x));
            
            string privateKey;
            byte[] scriptHash = null;
            
            do
            {
                Console.Write("Enter WIF private key: ");
                privateKey = Console.ReadLine();

                if (privateKey.Length == 52)
                {
                    break;
                }
            } while (true);

            do
            {
                Console.Write("Enter contract script hash or token symbol: ");
                var temp = Console.ReadLine();

                scriptHash = NeoAPI.GetScriptHashFromSymbol(temp);

                if (scriptHash == null && temp.Length == 40)
                {
                    scriptHash = NeoAPI.GetScriptHashFromString(temp);
                }

            } while (scriptHash == null);

            var keys = KeyPair.FromWIF(privateKey);

            var token = new NEP5(api, scriptHash);

            string fileName;

            do
            {
                Console.Write("Enter whitelist file name or NEO address: ");
                fileName = Console.ReadLine();

                if (!fileName.Contains("."))
                {
                    break;
                }

                if (File.Exists(fileName))
                {
                    break;
                }
            } while (true);

            List<string> lines;

            if (fileName.Contains("."))
            {
                lines = File.ReadAllLines(fileName).ToList();
            }
            else
            {
                lines = new List<string>() { fileName };
            }

            decimal amount;
            Console.WriteLine($"Write amount of {token.Symbol} to distribute to each address:");
            do
            {
                if (decimal.TryParse(Console.ReadLine(), out amount) && amount > 0)
                {
                    break;
                }
            } while (true);


            int skip = 0;
            int done = 0;

            Console.WriteLine($"Initializing {token.Name} airdrop...");

            var srcBalance = token.BalanceOf(keys);
            var minimum = lines.Count * amount;
            if (srcBalance < minimum)
            {
                Console.WriteLine($"Error: For this Airdrop you need at least {minimum} {token.Symbol} at {keys.address}");
                Console.ReadLine();
                return;
            }

            var oldBlock = api.GetBlockHeight();

            foreach (var temp in lines)
            {
                var address = temp.Trim();
                if (address.Length != 34 || !address.StartsWith("A"))
                {
                    skip++;
                    lines.RemoveAt(0);
                    Console.WriteLine("Invalid address: " + address);
                    continue;
                }

                var hash = address.GetScriptHashFromAddress();
                var balance = token.BalanceOf(hash);
                
                Console.WriteLine($"Found {address}: {balance} {token.Symbol}");

                Console.WriteLine($"Sending {token.Symbol} to  {address}");
                Transaction tx;

                do
                {
                    tx = token.Transfer(keys, address, amount);
                    Thread.Sleep(1000);

                } while (tx == null);

                Console.WriteLine("Unconfirmed transaction: " + tx.Hash);

                uint newBlock;

                do
                {
                    Thread.Sleep(5000);
                    newBlock = api.GetBlockHeight();
                } while (newBlock <= oldBlock);

                bool found = false;

                oldBlock++;
                while (oldBlock < newBlock)
                {
                    var other = api.GetBlock(oldBlock);

                    if (other != null)
                    {
                        foreach (var entry in other.transactions)
                        {
                            if (entry.Hash == tx.Hash)
                            {
                                found = true;
                                oldBlock = newBlock;
                                break;
                            }
                        }

                        oldBlock++;
                    }
                    else
                    {
                        Thread.Sleep(5000);
                    }

                }

                Console.WriteLine("Confirmed transaction: " + tx.Hash);

                /*Console.WriteLine($"Confirming balance: {address}. Should have {balance + amount} {token.Symbol} or more.");
                var newBalance = token.BalanceOf(hash);

                Console.WriteLine($"Got {newBalance} {token.Symbol}.");*/
            }

            Console.WriteLine($"Skipped {skip} invalid addresses.");
            Console.WriteLine($"Airdropped {amount} {token.Symbol} to {done} addresses.");

            Console.WriteLine("Finished.");
            Console.ReadLine();
        }
    }
}
