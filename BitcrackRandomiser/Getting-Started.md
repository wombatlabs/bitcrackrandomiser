# btcmultipuzzle.com client

Bitcrackrandomiser is a solo pool project for Bitcoin puzzle. It works with Bitcrack and VanitySearch.

Official client for "btcmultipuzzle.com pool".

Supports <ins>Windows</ins> and <ins>Linux</ins>. Supports <ins>NVIDIA</ins> devices.

## Related Links

Website | Link | Name
--- | --- | ---
Pool website | [btcmultipuzzle.com](https://btcmultipuzzle.com/)

### Build it yourself

You can build the client yourself.

- Bitcrack ([Go repo](https://github.com/brichard19/BitCrack))
- VanitySearch ([Go repo](https://github.com/ilkerccom/VanitySearch))
- VanitySearch (Optimised) ([Go repo](https://github.com/ilkerccom/VanitySearch-V2))
- Bitcrackrandomiser (This repo)

Endless thanks to everyone involved in the development of Bitcrack and VanitySearch applications.

## Quick start

1. Download the latest release and unzip file or build it yourself.
Windows - btcmultipool-windows.zip
Linux - btcmultipool-linux.zip

2. Download .NET 8.0 runtimes
Windows - download from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (.NET Runtime 8.x.x)
Linux - install with this prompt `sudo apt-get update && sudo apt-get install dotnet-sdk-8.0 -y`

3. Edit the <ins>[settings.txt](./BitcrackRandomiser/settings.txt)</ins> file to suit your machine's needs.

4. Run the application.
Windows - Run `BitcrackRandomiser.exe`
Linux - Run `dotnet BitcrackRandomiser.dll`

### Resuming previous session
Open up your log file and note the following line:
```
Backend credentials for worker####: client_id=SOME_ID | client_token=SOME_TOKEN
```

In your `setting.txt` file add the `client_id` to `backend_client_ids` and the `client_token` to the `backend_client_tokens` variables and save. Then when you restart your miner your info is remembered. 