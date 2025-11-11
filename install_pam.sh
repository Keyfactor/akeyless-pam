export RELEASE_VERSION=1.0.3-rc.3
export KEYFACTOR_HOME=/opt
export DLL_NAME=akeyless-pam.dll
export PAM_PROVIDER_URL=https://github.com/Keyfactor/akeyless-pam/releases/download/${RELEASE_VERSION}/akeyless-pam_${RELEASE_VERSION}.zip

sudo apt update && sudo apt install -y wget unzip

# download latest release from github 
wget $PAM_PROVIDER_URL -O akeyless-pam_$RELEASE_VERSION.zip

# unzip the release
unzip akeyless-pam_$RELEASE_VERSION.zip -d akeyless-pam_$RELEASE_VERSION

# copy the akeyless-pam.dll to the following locations

cp $DLL_NAME $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/KeyfactorAPI/bin
cp $DLL_NAME $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/WebAgentServices/bin
cp $DLL_NAME $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/Service


# Add the following line to the following XML files
# <register type="IPAMProvider" mapTo="Keyfactor.Extensions.Pam.Akeyless.Pam, akeyless-pam" name="Akeyless-" />

# Edit $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/KeyfactorAPI/bin/Web.config and add the following line to the <container> section
# Edit $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/Service/CMSTimerService.exe.config and add the following line to the <container> section
# Edit $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/WebConsole/Web.config and add the following line to the <container> section
# Edit $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/WebAgentServices/Web.config and add the following line to the <container> section

# Maybe???
# Edit $KEYFACTOR_HOME/Keyfactor/Keyfactor\ Platform/WebConsole/bin/CSS.CMS.Web.Console.dll.config and add the following line to the <container> section
