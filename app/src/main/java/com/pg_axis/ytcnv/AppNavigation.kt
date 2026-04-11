package com.pg_axis.ytcnv

import android.app.Application
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController

@Composable
fun AppNavigation(initialUrl: String? = null) {
    val navController = rememberNavController()
    val mainViewModel: MainViewModel = viewModel()

    LaunchedEffect(initialUrl) {
        if (!initialUrl.isNullOrBlank()) {
            mainViewModel.urlEntryText = initialUrl
        }
    }

    NavHost(navController = navController, startDestination = "main") {
        composable("main") {
            MainScreen(
                viewModel = mainViewModel,
                onOpenSearch = { navController.navigate("search") },
                onOpenSettings = { navController.navigate("settings") }
            )
        }
        composable("search") {
            val searchViewModel = remember {
                SearchViewModel(mainViewModel.settings)
            }
            SearchScreen(
                onBack = { navController.popBackStack() },
                onResultSelected = { url ->
                    mainViewModel.urlEntryText = url
                },
                viewModel = searchViewModel
            )
        }
        composable("settings") {
            val context = androidx.compose.ui.platform.LocalContext.current
            val settingsViewModel = remember {
                SettingsViewModel(mainViewModel.settings, mainViewModel, context.applicationContext as Application)
            }
            SettingsScreen(
                onBack = { navController.popBackStack() },
                viewModel = settingsViewModel
            )
        }
    }
}